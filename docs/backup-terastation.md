# TeraStationへのDBバックアップ

Home StockのPostgreSQLを毎日custom形式でdumpし、GPGで暗号化してTeraStation 3200DのSMB共有へ保存します。既定の保持期間は30日です。NASが未マウントの場合はローカルディスクへの誤保存を防ぐため失敗します。

## 1. TeraStationを設定する

設定画面の名称はファームウェアで多少異なります。次の構成を推奨します。

1. TeraStationへ固定IPまたはDHCP予約を設定し、時刻同期（NTP）と管理者通知を有効にします。管理者パスワードも初期値から変更します。
2. `ファイル共有` → `ユーザー`でローカルユーザー`homestock-backup`を作成し、十分に長いランダムパスワードを設定します。日常利用者やTeraStation管理者とは共用しません。
3. `ファイル共有` → `共有フォルダー`で`home-stock-backup`を作成します。公開プロトコルはSMBだけを有効にし、ゴミ箱は有効にします。Webアクセス、FTP、AFP、NFSは無効にします。
4. 共有フォルダーのアクセス制限を有効にし、`homestock-backup`だけを読み書き可能にします。ゲストアクセスと一般ユーザーのアクセスは許可しません。
5. SMB 1は無効にし、利用できる最も新しいSMBバージョンを使用します。NASの管理画面とSMBをインターネットへポート転送しません。

RAIDはディスク故障への対策であり、バックアップの代わりではありません。TeraStation自体の故障・盗難・ランサムウェアに備え、暗号化バックアップを定期的に別のUSB媒体または別拠点にも複製してください。

## 2. DebianからSMB共有をマウントする

必要なパッケージを導入します。

```bash
sudo apt update
sudo apt install --yes cifs-utils gnupg
```

資格情報ファイルを作成します。パスワードを`/etc/fstab`やリポジトリへ直接書かないでください。

```bash
sudo install -d -m 0700 /etc/home-stock
sudoedit /etc/home-stock/terastation.credentials
sudo chmod 0600 /etc/home-stock/terastation.credentials
```

内容は次の2行です。

```ini
username=homestock-backup
password=<TeraStationで設定したパスワード>
```

マウント先を作り、`/etc/fstab`へ追加します。`192.168.100.30`はTeraStationの実際のIPへ置き換えます。`homestock`はHome Stockを実行するLinuxユーザーです。

```bash
sudo install -d -o homestock -g homestock -m 0700 /mnt/terastation-home-stock
sudoedit /etc/fstab
```

```fstab
//192.168.100.30/home-stock-backup /mnt/terastation-home-stock cifs credentials=/etc/home-stock/terastation.credentials,vers=3.0,uid=homestock,gid=homestock,file_mode=0600,dir_mode=0700,nosuid,nodev,noexec,_netdev,x-systemd.automount,x-systemd.idle-timeout=60 0 0
```

TS3200D側がSMB 3.0を受け付けない場合だけ`vers=2.1`へ下げます。SMB 1.0は使用しません。接続を確認し、バックアップ用サブディレクトリを作成します。

```bash
sudo systemctl daemon-reload
sudo mount /mnt/terastation-home-stock
sudo -u homestock mkdir /mnt/terastation-home-stock/home-stock
sudo -u homestock touch /mnt/terastation-home-stock/home-stock/write-test
sudo -u homestock rm /mnt/terastation-home-stock/home-stock/write-test
```

## 3. 暗号鍵を用意する

管理用PCなどDebianサーバーとは別の安全な端末で暗号鍵を作成します。復号用秘密鍵はパスワードマネージャーとオフライン媒体へ保管し、バックアップを実行するDebianサーバーには公開鍵だけを配置します。

```bash
gpg --quick-generate-key 'Home Stock Backup <backup@home-stock.local>' rsa3072 encrypt 3y
gpg --armor --export 'Home Stock Backup' > home-stock-backup-public-key.asc
gpg --armor --export-secret-keys 'Home Stock Backup' > home-stock-backup-private-key.asc
```

秘密鍵ファイルを安全なオフライン媒体へ移し、作業端末上のエクスポートファイルを安全に削除します。公開鍵だけをDebianへコピーしてインポートし、フィンガープリントを確認します。

```bash
sudo -u homestock gpg --import home-stock-backup-public-key.asc
sudo -u homestock gpg --list-keys --with-colons 'Home Stock Backup'
```

`fpr`行に表示されたフィンガープリントを設定に使用します。期限前に新しい鍵へ切り替えてください。過去のバックアップを復号できるよう、古い秘密鍵も保持期間中は安全に保管します。

## 4. バックアップを試す

`/etc/home-stock/backup.env`を作成します。値に空白を含めず、シェル構文は記述しません。ファイルの所有者を`root`、グループを`homestock`、権限を`0640`にします。

```dotenv
NAS_MOUNT_POINT=/mnt/terastation-home-stock
BACKUP_SUBDIRECTORY=home-stock
BACKUP_RETENTION_DAYS=30
BACKUP_GPG_RECIPIENT=<GPG鍵のフィンガープリント>
POSTGRES_DB=homestock
POSTGRES_USER=homestock
```

```bash
sudo chown root:homestock /etc/home-stock/backup.env
sudo chmod 0640 /etc/home-stock/backup.env
cd /opt/home-stock
sudo -u homestock sh -c 'set -a; . /etc/home-stock/backup.env; set +a; exec sh ./deploy/backup-to-nas.sh'
```

成功時は`.dump.gpg`と`.sha256`が作成されます。失敗時は終了コードが0以外になり、未完成の`.partial`は削除されます。

## 5. 日次実行を有効にする

付属のsystemd unitは、リポジトリが`/opt/home-stock`、実行ユーザーが`homestock`である前提です。異なる場合はserviceファイルを変更します。

`homestock`ユーザーからDockerを操作できる必要があります。Dockerグループへの所属はroot相当の権限を与えるため、このユーザーを対話ログインや他用途に共用しないでください。より厳密に分離する場合は、rootでserviceを実行し、unitの保護設定を別途設計します。

```bash
sudo cp deploy/home-stock-backup.service deploy/home-stock-backup.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now home-stock-backup.timer
sudo systemctl start home-stock-backup.service
sudo systemctl status home-stock-backup.service
sudo systemctl list-timers home-stock-backup.timer
```

失敗を見逃さないよう、サーバーの監視から`systemctl is-failed home-stock-backup.service`を監視するか、systemdの失敗通知を設定します。

## 6. 月次で復元を確認する

秘密鍵を保管している隔離された管理端末または復元試験用ホストで実施します。NAS上のファイルを直接復元せず、まずチェックサムを確認してローカルの一時領域へ復号します。次の例は、そのホストにリポジトリ、Docker、復号鍵が用意され、TeraStationが同じパスへマウント済みである前提です。

```bash
cd /mnt/terastation-home-stock/home-stock
sha256sum --check homestock-<timestamp>.dump.gpg.sha256
gpg --output /tmp/homestock-restore-test.dump --decrypt homestock-<timestamp>.dump.gpg
cd /opt/home-stock
sh ./deploy/restore-test.sh /tmp/homestock-restore-test.dump
rm /tmp/homestock-restore-test.dump
```

復元確認後は、平文dumpを必ず削除します。本番DBそのものへの復元は既存データを上書きし得るため、この手順では行いません。

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BCrypt.Net;

class Program
{
    private static readonly HttpClient client = new HttpClient();

    static async Task Main()
    {
        Console.WriteLine("🔹 ユーザー登録を開始します 🔹");

        // ユーザー名を入力
        Console.Write("ユーザー名: ");
        string username = Console.ReadLine()!.Trim();

        // パスワードを入力（非表示）
        Console.Write("パスワード: ");
        string password = ReadPassword();

        // パスワードをハッシュ化
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        // JSONデータを作成
        var user = new { Username = username, Password_Hash = hashedPassword };
        string jsonData = JsonSerializer.Serialize(user);
        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

        // APIへ送信
        try
        {
            HttpResponseMessage response = await client.PostAsync("https://localhost:44394/api/users/register", content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("\n✅ ユーザー登録成功！");
            }
            else
            {
                Console.WriteLine($"\n❌ 登録失敗: {response.StatusCode}");
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"エラー詳細: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ エラー: {ex.Message}");
        }
    }

    // パスワード入力時に非表示にするメソッド
    private static string ReadPassword()
    {
        StringBuilder password = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b"); // バックスペースで1文字削除
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write("*"); // 入力を `*` で隠す
            }
        }
        Console.WriteLine();
        return password.ToString();
    }
}

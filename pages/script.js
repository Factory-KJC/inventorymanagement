const apiUrl = "https://localhost:44394/api/inventory";
const authUrl = "https://localhost:44394/api/auth/login";
let token = "";

// ログイン処理
async function login() {
    const username = document.getElementById("username").value.trim();
    const password = document.getElementById("password").value.trim();

    if (!username || !password) {
        alert("ユーザー名とパスワードを入力してください。");
        return;
    }

    const response = await fetch(authUrl, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password })
    });

    if (!response.ok) {
        alert("ログインに失敗しました。");
        return;
    }

    const data = await response.json();
    token = data.token;
    document.getElementById("loginStatus").innerText = "ログイン成功";
    enableInventoryControls();
    loadInventory();
}

// 在庫リストを取得
async function loadInventory() {
    const response = await fetch(apiUrl, {
        headers: { "Authorization": `Bearer ${token}` }
    });

    if (!response.ok) {
        alert("在庫データの取得に失敗しました。");
        return;
    }

    const items = await response.json();
    const list = document.getElementById("inventoryList");
    list.innerHTML = "";

    items.forEach(item => {
        const div = document.createElement("div");
        div.classList.add("inventory-item");
        div.innerHTML = `
            <span>${item.name} (${item.quantity}) </span>
            <svg id="barcode-${item.barcode}"></svg>
            <button class="delete" onclick="deleteItem(${item.id})">削除</button>
        `;
        list.appendChild(div);

        JsBarcode(`#barcode-${item.barcode}`, item.barcode, {
            format: "EAN13",  // EAN13が準標的なJANコード形式
            lineColor: "#000",
            width: 2,
            height: 50,
            displayValue: true
        });
    });
}

// 在庫を追加
async function addItem() {
    const name = document.getElementById("itemName").value.trim();
    const quantity = document.getElementById("itemQuantity").value;
    const barcode = document.getElementById("itemBarcode").value;

    if (!name || quantity <= 0) {
        alert("正しい商品名と数量を入力してください");
        return;
    }

    await fetch(apiUrl, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "Authorization": `Bearer ${token}`
        },
        body: JSON.stringify({ name, quantity: parseInt(quantity), barcode })
    });

    document.getElementById("itemName").value = "";
    document.getElementById("itemQuantity").value = "";
    document.getElementById("itemBarcode").value = ""
    loadInventory();
}

// 在庫を削除
async function deleteItem(id) {
    await fetch(`${apiUrl}/${id}`, {
        method: "DELETE",
        headers: { "Authorization": `Bearer ${token}` }
    });
    loadInventory();
}


// ログイン成功後に在庫管理のUIを有効化
function enableInventoryControls() {
    document.getElementById("itemName").disabled = false;
    document.getElementById("itemQuantity").disabled = false;
    document.getElementById("itemBarcode").disabled = false;
    document.querySelector("button[onclick='addItem()']").disabled = false;
}

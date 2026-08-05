const API = Object.freeze({
  dashboard: "/api/dashboard",
  products: "/api/products",
  locations: "/api/locations",
  inventory: "/api/inventory",
  shoppingList: "/api/shopping-lists/current",
  login: "/api/auth/login",
  register: "/api/users/register",
  receiveStock: "/api/inventory/receive",
  consumeStock: "/api/inventory/consume"
});

const STORAGE_KEYS = Object.freeze({
  token: "home-stock-token",
  commandDatabase: "home-stock",
  commandStore: "commands"
});

const DASHBOARD_TITLES = Object.freeze({
  products: "登録商品",
  "low-stock": "在庫不足",
  expiring: "7日以内期限",
  shopping: "買うもの"
});

const state = {
  token: sessionStorage.getItem(STORAGE_KEYS.token),
  products: [],
  locations: [],
  inventory: [],
  shopping: null,
  dashboardList: null
};

const $ = selector => document.querySelector(selector);
const $$ = selector => [...document.querySelectorAll(selector)];
let toastTimer;

const escapeHtml = value => String(value ?? "").replace(
  /[&<>'"]/g,
  character => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "'": "&#39;",
    '"': "&quot;"
  })[character]
);

// -----------------------------------------------------------------------------
// API通信と共通UI
// -----------------------------------------------------------------------------

async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (state.token) {
    headers.set("Authorization", `Bearer ${state.token}`);
  }
  if (options.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(path, { ...options, headers });
  if (response.status === 401) {
    logout();
    throw new Error("ログインの有効期限が切れました。");
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.title || problem.message || `通信に失敗しました (${response.status})`);
  }

  return response.status === 204 ? null : response.json();
}

function toast(message) {
  const element = $("#toast");
  element.textContent = message;
  element.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => {
    element.hidden = true;
  }, 2800);
}

function showApp(authenticated) {
  $("#login-view").hidden = authenticated;
  $("#app-view").hidden = !authenticated;
  if (authenticated) {
    refreshAll();
  }
}

function logout() {
  state.token = null;
  sessionStorage.removeItem(STORAGE_KEYS.token);
  showApp(false);
}

async function refreshAll() {
  try {
    const [dashboard, products, locations, inventory, shopping] = await Promise.all([
      api(API.dashboard),
      api(API.products),
      api(API.locations),
      api(API.inventory),
      api(API.shoppingList)
    ]);

    state.products = products;
    state.locations = locations;
    state.inventory = inventory;
    state.shopping = shopping;

    renderDashboard(dashboard);
    renderInventory();
    renderShopping();
    fillSelectors();
  } catch (error) {
    toast(error.message);
  }
}

// -----------------------------------------------------------------------------
// ダッシュボードと一覧描画
// -----------------------------------------------------------------------------

function renderDashboard(data) {
  const statistics = [
    [data.productCount, "登録商品", "products"],
    [data.lowStockCount, "在庫不足", "low-stock"],
    [data.expiringSoonCount, "7日以内期限", "expiring"],
    [data.shoppingItemCount, "買うもの", "shopping"]
  ];

  $("#stats").innerHTML = statistics
    .map(([value, label, category]) => `
      <button type="button" class="stat-card" data-dashboard="${category}">
        <strong>${value}</strong><span>${label}</span>
      </button>`)
    .join("");
  $$('[data-dashboard]').forEach(button => {
    button.onclick = () => openDashboardList(button.dataset.dashboard);
  });

  const today = new Date();
  const limit = new Date();
  limit.setDate(today.getDate() + 7);
  const expiring = state.inventory
    .filter(item => item.expiresOn && new Date(`${item.expiresOn}T00:00:00`) <= limit)
    .slice(0, 4);
  $("#expiring-list").innerHTML = expiring.length
    ? expiring.map(inventoryCard).join("")
    : emptyCard("期限間近の商品はありません");
}

async function openDashboardList(category, page = 1) {
  try {
    state.dashboardList = await api(`${API.dashboard}/${category}?page=${page}&pageSize=20`);
    const { items, totalCount, pageSize } = state.dashboardList;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

    $("#dashboard-list-title").textContent = DASHBOARD_TITLES[category] || "一覧";
    $("#dashboard-list").innerHTML = items.length
      ? items.map(dashboardItemCard).join("")
      : emptyCard("表示する項目はありません");
    $("#dashboard-page-state").textContent =
      `${state.dashboardList.page} / ${totalPages}ページ（全${totalCount}件）`;
    $("#dashboard-prev").disabled = state.dashboardList.page <= 1;
    $("#dashboard-next").disabled = state.dashboardList.page >= totalPages;

    const dialog = $("#dashboard-dialog");
    if (!dialog.open) {
      dialog.showModal();
    }
  } catch (error) {
    toast(error.message);
  }
}

function dashboardItemCard(item) {
  const quantity = item.quantity == null
    ? ""
    : `<span class="quantity">${item.quantity}</span>`;
  return `
    <article class="item-card">
      <div><h3>${escapeHtml(item.name)}</h3><p>${escapeHtml(item.detail)}</p></div>
      ${quantity}
    </article>`;
}

function inventoryCard(item) {
  const expiration = item.expiresOn ? ` · 期限 ${escapeHtml(item.expiresOn)}` : "";
  return `
    <article class="item-card">
      <div>
        <h3>${escapeHtml(item.productName)}</h3>
        <p>${escapeHtml(item.locationName)}${expiration}</p>
      </div>
      <div>
        <div class="quantity">${item.quantity} ${escapeHtml(item.unit)}</div>
        <div class="item-actions">
          <button class="small-button consume" data-product="${item.productId}">消費</button>
        </div>
      </div>
    </article>`;
}

function renderInventory() {
  const query = $("#inventory-search").value.trim().toLowerCase();
  const rows = state.inventory.filter(item =>
    !query ||
    item.productName.toLowerCase().includes(query) ||
    (item.barcode || "").includes(query)
  );

  $("#inventory-list").innerHTML = rows.length
    ? rows.map(inventoryCard).join("")
    : emptyCard("該当する在庫はありません");
  $$(".consume").forEach(button => {
    button.onclick = () => consumeProduct(button.dataset.product);
  });
}

function renderShopping() {
  const items = state.shopping?.items || [];
  $("#shopping-list").innerHTML = items.length
    ? items.map(shoppingItemCard).join("")
    : emptyCard("買い物リストは空です");

  $$(".shopping-toggle").forEach(button => {
    button.onclick = () => toggleShoppingItem(button);
  });
}

function shoppingItemCard(item) {
  const completed = item.status === "Purchased";
  const source = item.source === "ReorderSuggestion" ? "在庫から提案" : "手動追加";
  return `
    <article class="item-card ${completed ? "shopping-done" : ""}">
      <div><h3>${escapeHtml(item.name)}</h3><p>${source}</p></div>
      <div class="item-actions">
        <span class="quantity">${item.quantity}</span>
        <button class="small-button shopping-toggle" data-id="${item.id}" data-status="${item.status}">
          ${completed ? "戻す" : "購入"}
        </button>
      </div>
    </article>`;
}

async function toggleShoppingItem(button) {
  const status = button.dataset.status === "Purchased" ? "Pending" : "Purchased";
  try {
    state.shopping = await api(`${API.shoppingList}/items/${button.dataset.id}`, {
      method: "PATCH",
      body: JSON.stringify({ status })
    });
    renderShopping();
  } catch (error) {
    toast(error.message);
  }
}

function emptyCard(message) {
  return `<div class="item-card"><p>${escapeHtml(message)}</p></div>`;
}

function fillSelectors() {
  $("#stock-product").innerHTML = state.products
    .map(product => `<option value="${product.id}">${escapeHtml(product.name)}</option>`)
    .join("");
  $("#stock-location").innerHTML = state.locations
    .map(location => `<option value="${location.id}">${escapeHtml(location.name)}</option>`)
    .join("");
}

// -----------------------------------------------------------------------------
// 在庫操作
// -----------------------------------------------------------------------------

async function consumeProduct(productId) {
  const product = state.products.find(candidate => candidate.id === productId);
  const quantity = prompt(`${product?.name || "商品"}の消費数量`, "1");
  if (!quantity || Number(quantity) <= 0) {
    return;
  }

  await sendStockCommand(API.consumeStock, { productId, quantity: Number(quantity) });
}

async function sendStockCommand(path, body) {
  // UUIDを冪等キーとして保存し、再送時も同じ在庫操作として扱います。
  const command = {
    id: crypto.randomUUID(),
    path,
    body,
    createdAt: new Date().toISOString()
  };

  try {
    await api(path, {
      method: "POST",
      headers: { "Idempotency-Key": command.id },
      body: JSON.stringify(body)
    });
    toast("在庫を更新しました");
    await refreshAll();
  } catch (error) {
    if (!navigator.onLine || error instanceof TypeError) {
      await enqueue(command);
      toast("オフラインのため再送待ちに保存しました");
      await updateNetwork();
      return;
    }
    toast(error.message);
  }
}

function openDialog(id) {
  if (!state.products.length && id === "stock-dialog") {
    toast("先に商品を登録してください");
    return;
  }
  $(`#${id}`).showModal();
}

// -----------------------------------------------------------------------------
// フォームと画面遷移
// -----------------------------------------------------------------------------

async function handleLogin(event) {
  event.preventDefault();
  $("#login-error").textContent = "";
  try {
    const result = await api(API.login, {
      method: "POST",
      body: JSON.stringify({
        username: $("#username").value,
        password: $("#password").value
      })
    });
    state.token = result.token;
    sessionStorage.setItem(STORAGE_KEYS.token, state.token);
    showApp(true);
  } catch (error) {
    $("#login-error").textContent = error.message;
  }
}

async function registerInitialUser() {
  try {
    await api(API.register, {
      method: "POST",
      body: JSON.stringify({
        username: $("#username").value,
        password: $("#password").value
      })
    });
    toast("登録しました。ログインしてください");
  } catch (error) {
    $("#login-error").textContent = error.message;
  }
}

function changePage(page) {
  $$(".page").forEach(element => {
    element.classList.toggle("active", element.id === `${page}-page`);
  });
  $$('[data-page]').forEach(element => {
    element.classList.toggle("active", element.dataset.page === page);
  });
  $("#page-title").textContent = ({
    home: "ホーム",
    inventory: "在庫",
    shopping: "買い物"
  })[page];
}

async function handleProductSubmit(event) {
  event.preventDefault();
  const value = selector => $(selector).value.trim();
  const optionalNumber = selector => value(selector) ? Number(value(selector)) : null;

  try {
    await api(API.products, {
      method: "POST",
      body: JSON.stringify({
        name: value("#product-name"),
        barcode: value("#product-barcode") || null,
        unit: value("#product-unit"),
        reorderPoint: optionalNumber("#reorder-point"),
        targetQuantity: optionalNumber("#target-quantity")
      })
    });
    $("#product-dialog").close();
    event.target.reset();
    $("#product-unit").value = "個";
    toast("商品を登録しました");
    await refreshAll();
  } catch (error) {
    toast(error.message);
  }
}

async function handleStockSubmit(event) {
  event.preventDefault();
  $("#stock-dialog").close();
  await sendStockCommand(API.receiveStock, {
    productId: $("#stock-product").value,
    locationId: $("#stock-location").value,
    quantity: Number($("#stock-quantity").value),
    expiresOn: $("#stock-expiry").value || null
  });
}

async function handleShoppingSubmit(event) {
  event.preventDefault();
  try {
    state.shopping = await api(`${API.shoppingList}/items`, {
      method: "POST",
      body: JSON.stringify({
        name: $("#shopping-name").value,
        quantity: Number($("#shopping-quantity").value)
      })
    });
    event.target.reset();
    $("#shopping-quantity").value = 1;
    renderShopping();
  } catch (error) {
    toast(error.message);
  }
}

async function generateShoppingList() {
  try {
    state.shopping = await api(`${API.shoppingList}/generate`, { method: "POST" });
    renderShopping();
    toast("不足品を更新しました");
  } catch (error) {
    toast(error.message);
  }
}

// -----------------------------------------------------------------------------
// オフライン操作キュー
// -----------------------------------------------------------------------------

function openQueue() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(STORAGE_KEYS.commandDatabase, 1);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(STORAGE_KEYS.commandStore)) {
        request.result.createObjectStore(STORAGE_KEYS.commandStore, { keyPath: "id" });
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function enqueue(command) {
  const database = await openQueue();
  const transaction = database.transaction(STORAGE_KEYS.commandStore, "readwrite");
  transaction.objectStore(STORAGE_KEYS.commandStore).put(command);
  return transactionCompleted(transaction);
}

async function queuedCommands() {
  const database = await openQueue();
  return new Promise((resolve, reject) => {
    const request = database
      .transaction(STORAGE_KEYS.commandStore)
      .objectStore(STORAGE_KEYS.commandStore)
      .getAll();
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function removeQueued(id) {
  const database = await openQueue();
  const transaction = database.transaction(STORAGE_KEYS.commandStore, "readwrite");
  transaction.objectStore(STORAGE_KEYS.commandStore).delete(id);
  return transactionCompleted(transaction);
}

function transactionCompleted(transaction) {
  return new Promise((resolve, reject) => {
    transaction.oncomplete = resolve;
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error);
  });
}

async function replayQueue() {
  if (!state.token || !navigator.onLine) {
    return;
  }

  for (const command of await queuedCommands()) {
    try {
      await api(command.path, {
        method: "POST",
        headers: { "Idempotency-Key": command.id },
        body: JSON.stringify(command.body)
      });
      await removeQueued(command.id);
    } catch (error) {
      if (error instanceof TypeError) {
        break;
      }

      // 4xxなど再送しても成功しない操作はキューを詰まらせないため破棄します。
      await removeQueued(command.id);
      toast(`再送できない操作を破棄しました: ${error.message}`);
    }
  }

  await refreshAll();
  await updateNetwork();
}

async function updateNetwork() {
  const count = (await queuedCommands().catch(() => [])).length;
  const online = navigator.onLine;
  const element = $("#network-state");
  element.classList.toggle("offline", !online);
  element.textContent = online
    ? (count ? `再送待ち ${count}件` : "オンライン")
    : "オフライン";
}

function bindEvents() {
  $("#login-form").addEventListener("submit", handleLogin);
  $("#setup-button").onclick = registerInitialUser;
  $("#logout").onclick = logout;
  $("#inventory-search").oninput = renderInventory;
  $("#quick-receive").onclick = $("#open-receive").onclick = () => openDialog("stock-dialog");
  $("#quick-product").onclick = () => openDialog("product-dialog");
  $("#product-form").onsubmit = handleProductSubmit;
  $("#stock-form").onsubmit = handleStockSubmit;
  $("#shopping-form").onsubmit = handleShoppingSubmit;
  $("#generate-shopping").onclick = generateShoppingList;

  $$('[data-page]').forEach(button => {
    button.onclick = () => changePage(button.dataset.page);
  });
  $$(".close-dialog").forEach(button => {
    button.onclick = () => button.closest("dialog").close();
  });
  $("#dashboard-prev").onclick = () => {
    if (state.dashboardList?.page > 1) {
      openDashboardList(state.dashboardList.category, state.dashboardList.page - 1);
    }
  };
  $("#dashboard-next").onclick = () => {
    if (state.dashboardList) {
      openDashboardList(state.dashboardList.category, state.dashboardList.page + 1);
    }
  };

  window.addEventListener("online", () => {
    updateNetwork();
    replayQueue();
  });
  window.addEventListener("offline", updateNetwork);
}

function initialize() {
  bindEvents();
  if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("/service-worker.js");
  }
  updateNetwork();
  showApp(Boolean(state.token));
  if (state.token) {
    replayQueue();
  }
}

initialize();

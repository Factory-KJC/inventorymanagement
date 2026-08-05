const state = { token: sessionStorage.getItem("home-stock-token"), products: [], locations: [], inventory: [], shopping: null, dashboardList: null };
const $ = selector => document.querySelector(selector);
const $$ = selector => [...document.querySelectorAll(selector)];
const escapeHtml = value => String(value ?? "").replace(/[&<>'"]/g, char => ({"&":"&amp;","<":"&lt;",">":"&gt;","'":"&#39;",'"':"&quot;"}[char]));

async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (state.token) headers.set("Authorization", `Bearer ${state.token}`);
  if (options.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  const response = await fetch(path, { ...options, headers });
  if (response.status === 401) { logout(); throw new Error("ログインの有効期限が切れました。"); }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.title || problem.message || `通信に失敗しました (${response.status})`);
  }
  return response.status === 204 ? null : response.json();
}

function toast(message) {
  const element = $("#toast"); element.textContent = message; element.hidden = false;
  clearTimeout(toast.timer); toast.timer = setTimeout(() => element.hidden = true, 2800);
}

function showApp(authenticated) {
  $("#login-view").hidden = authenticated;
  $("#app-view").hidden = !authenticated;
  if (authenticated) refreshAll();
}

function logout() {
  state.token = null; sessionStorage.removeItem("home-stock-token"); showApp(false);
}

async function refreshAll() {
  try {
    const [dashboard, products, locations, inventory, shopping] = await Promise.all([
      api("/api/dashboard"), api("/api/products"), api("/api/locations"), api("/api/inventory"), api("/api/shopping-lists/current")
    ]);
    state.products = products; state.locations = locations; state.inventory = inventory; state.shopping = shopping;
    renderDashboard(dashboard); renderInventory(); renderShopping(); fillSelectors();
  } catch (error) { toast(error.message); }
}

function renderDashboard(data) {
  const stats = [[data.productCount,"登録商品","products"],[data.lowStockCount,"在庫不足","low-stock"],[data.expiringSoonCount,"7日以内期限","expiring"],[data.shoppingItemCount,"買うもの","shopping"]];
  $("#stats").innerHTML = stats.map(([value,label,category]) => `<button type="button" class="stat-card" data-dashboard="${category}"><strong>${value}</strong><span>${label}</span></button>`).join("");
  $$('[data-dashboard]').forEach(button => button.onclick = () => openDashboardList(button.dataset.dashboard));
  const today = new Date(); const limit = new Date(); limit.setDate(today.getDate() + 7);
  const expiring = state.inventory.filter(x => x.expiresOn && new Date(`${x.expiresOn}T00:00:00`) <= limit).slice(0,4);
  $("#expiring-list").innerHTML = expiring.length ? expiring.map(inventoryCard).join("") : emptyCard("期限間近の商品はありません");
}

const dashboardTitles = { products:"登録商品", "low-stock":"在庫不足", expiring:"7日以内期限", shopping:"買うもの" };

async function openDashboardList(category, page = 1) {
  try {
    state.dashboardList = await api(`/api/dashboard/${category}?page=${page}&pageSize=20`);
    $("#dashboard-list-title").textContent = dashboardTitles[category] || "一覧";
    $("#dashboard-list").innerHTML = state.dashboardList.items.length
      ? state.dashboardList.items.map(item => `<article class="item-card"><div><h3>${escapeHtml(item.name)}</h3><p>${escapeHtml(item.detail)}</p></div>${item.quantity == null ? "" : `<span class="quantity">${item.quantity}</span>`}</article>`).join("")
      : emptyCard("表示する項目はありません");
    const totalPages = Math.max(1, Math.ceil(state.dashboardList.totalCount / state.dashboardList.pageSize));
    $("#dashboard-page-state").textContent = `${state.dashboardList.page} / ${totalPages}ページ（全${state.dashboardList.totalCount}件）`;
    $("#dashboard-prev").disabled = state.dashboardList.page <= 1;
    $("#dashboard-next").disabled = state.dashboardList.page >= totalPages;
    if (!$("#dashboard-dialog").open) $("#dashboard-dialog").showModal();
  } catch (error) { toast(error.message); }
}

function inventoryCard(item) {
  return `<article class="item-card"><div><h3>${escapeHtml(item.productName)}</h3><p>${escapeHtml(item.locationName)}${item.expiresOn ? ` · 期限 ${escapeHtml(item.expiresOn)}` : ""}</p></div><div><div class="quantity">${item.quantity} ${escapeHtml(item.unit)}</div><div class="item-actions"><button class="small-button consume" data-product="${item.productId}">消費</button></div></div></article>`;
}

function renderInventory() {
  const query = $("#inventory-search").value.trim().toLowerCase();
  const rows = state.inventory.filter(x => !query || x.productName.toLowerCase().includes(query) || (x.barcode || "").includes(query));
  $("#inventory-list").innerHTML = rows.length ? rows.map(inventoryCard).join("") : emptyCard("該当する在庫はありません");
  $$(".consume").forEach(button => button.onclick = () => consumeProduct(button.dataset.product));
}

function renderShopping() {
  const items = state.shopping?.items || [];
  $("#shopping-list").innerHTML = items.length ? items.map(item => `<article class="item-card ${item.status === "Purchased" ? "shopping-done" : ""}"><div><h3>${escapeHtml(item.name)}</h3><p>${item.source === "ReorderSuggestion" ? "在庫から提案" : "手動追加"}</p></div><div class="item-actions"><span class="quantity">${item.quantity}</span><button class="small-button shopping-toggle" data-id="${item.id}" data-status="${item.status}">${item.status === "Purchased" ? "戻す" : "購入"}</button></div></article>`).join("") : emptyCard("買い物リストは空です");
  $$(".shopping-toggle").forEach(button => button.onclick = async () => {
    try { state.shopping = await api(`/api/shopping-lists/current/items/${button.dataset.id}`, { method:"PATCH", body:JSON.stringify({ status: button.dataset.status === "Purchased" ? "Pending" : "Purchased" }) }); renderShopping(); }
    catch (error) { toast(error.message); }
  });
}

function emptyCard(message) { return `<div class="item-card"><p>${escapeHtml(message)}</p></div>`; }

function fillSelectors() {
  $("#stock-product").innerHTML = state.products.map(x => `<option value="${x.id}">${escapeHtml(x.name)}</option>`).join("");
  $("#stock-location").innerHTML = state.locations.map(x => `<option value="${x.id}">${escapeHtml(x.name)}</option>`).join("");
}

async function consumeProduct(productId) {
  const product = state.products.find(x => x.id === productId);
  const quantity = prompt(`${product?.name || "商品"}の消費数量`, "1");
  if (!quantity || Number(quantity) <= 0) return;
  await sendStockCommand("/api/inventory/consume", { productId, quantity:Number(quantity) });
}

async function sendStockCommand(path, body) {
  const command = { id: crypto.randomUUID(), path, body, createdAt:new Date().toISOString() };
  try {
    await api(path, { method:"POST", headers:{"Idempotency-Key":command.id}, body:JSON.stringify(body) });
    toast("在庫を更新しました"); await refreshAll();
  } catch (error) {
    if (!navigator.onLine || error instanceof TypeError) { await enqueue(command); toast("オフラインのため再送待ちに保存しました"); updateNetwork(); return; }
    toast(error.message);
  }
}

function openDialog(id) { if (!state.products.length && id === "stock-dialog") return toast("先に商品を登録してください"); $(`#${id}`).showModal(); }

$("#login-form").addEventListener("submit", async event => {
  event.preventDefault(); $("#login-error").textContent = "";
  try {
    const result = await api("/api/auth/login", { method:"POST", body:JSON.stringify({username:$("#username").value,password:$("#password").value}) });
    state.token = result.token; sessionStorage.setItem("home-stock-token", state.token); showApp(true);
  } catch (error) { $("#login-error").textContent = error.message; }
});

$("#setup-button").onclick = async () => {
  try { await api("/api/users/register", {method:"POST",body:JSON.stringify({username:$("#username").value,password:$("#password").value})}); toast("登録しました。ログインしてください"); }
  catch(error) { $("#login-error").textContent = error.message; }
};
$("#logout").onclick = logout;

function changePage(page) {
  $$(".page").forEach(x => x.classList.toggle("active", x.id === `${page}-page`));
  $$('[data-page]').forEach(x => x.classList.toggle("active", x.dataset.page === page));
  $("#page-title").textContent = ({home:"ホーム",inventory:"在庫",shopping:"買い物"})[page];
}
$$('[data-page]').forEach(button => button.onclick = () => changePage(button.dataset.page));
$("#inventory-search").oninput = renderInventory;
$("#quick-receive").onclick = $("#open-receive").onclick = () => openDialog("stock-dialog");
$("#quick-product").onclick = () => openDialog("product-dialog");
$$('.close-dialog').forEach(button => button.onclick = () => button.closest("dialog").close());
$("#dashboard-prev").onclick = () => {
  if (state.dashboardList && state.dashboardList.page > 1) {
    openDashboardList(state.dashboardList.category, state.dashboardList.page - 1);
  }
};
$("#dashboard-next").onclick = () => {
  if (state.dashboardList) {
    openDashboardList(state.dashboardList.category, state.dashboardList.page + 1);
  }
};

$("#product-form").onsubmit = async event => {
  event.preventDefault();
  const value = id => $(id).value.trim();
  try {
    await api("/api/products", {method:"POST",body:JSON.stringify({name:value("#product-name"),barcode:value("#product-barcode")||null,unit:value("#product-unit"),reorderPoint:value("#reorder-point")?Number(value("#reorder-point")):null,targetQuantity:value("#target-quantity")?Number(value("#target-quantity")):null})});
    $("#product-dialog").close(); event.target.reset(); $("#product-unit").value="個"; toast("商品を登録しました"); await refreshAll();
  } catch(error) { toast(error.message); }
};

$("#stock-form").onsubmit = async event => {
  event.preventDefault(); $("#stock-dialog").close();
  await sendStockCommand("/api/inventory/receive", {productId:$("#stock-product").value,locationId:$("#stock-location").value,quantity:Number($("#stock-quantity").value),expiresOn:$("#stock-expiry").value||null});
};

$("#shopping-form").onsubmit = async event => {
  event.preventDefault();
  try { state.shopping = await api("/api/shopping-lists/current/items", {method:"POST",body:JSON.stringify({name:$("#shopping-name").value,quantity:Number($("#shopping-quantity").value)})}); event.target.reset(); $("#shopping-quantity").value=1; renderShopping(); }
  catch(error) { toast(error.message); }
};
$("#generate-shopping").onclick = async () => { try { state.shopping = await api("/api/shopping-lists/current/generate",{method:"POST"}); renderShopping(); toast("不足品を更新しました"); } catch(error){toast(error.message);} };

function openQueue() { return new Promise((resolve,reject) => { const request=indexedDB.open("home-stock",1); request.onupgradeneeded=()=>request.result.createObjectStore("commands",{keyPath:"id"}); request.onsuccess=()=>resolve(request.result); request.onerror=()=>reject(request.error); }); }
async function enqueue(command) { const db=await openQueue(); const tx=db.transaction("commands","readwrite"); tx.objectStore("commands").put(command); return new Promise((resolve,reject)=>{tx.oncomplete=resolve;tx.onerror=()=>reject(tx.error);}); }
async function queuedCommands() { const db=await openQueue(); return new Promise((resolve,reject)=>{const request=db.transaction("commands").objectStore("commands").getAll();request.onsuccess=()=>resolve(request.result);request.onerror=()=>reject(request.error);}); }
async function removeQueued(id) { const db=await openQueue(); const tx=db.transaction("commands","readwrite");tx.objectStore("commands").delete(id);return new Promise(resolve=>tx.oncomplete=resolve); }
async function replayQueue() {
  if (!state.token || !navigator.onLine) return;
  for (const command of await queuedCommands()) {
    try { await api(command.path,{method:"POST",headers:{"Idempotency-Key":command.id},body:JSON.stringify(command.body)}); await removeQueued(command.id); }
    catch(error) { if(error instanceof TypeError) break; await removeQueued(command.id); toast(`再送できない操作を破棄しました: ${error.message}`); }
  }
  await refreshAll(); updateNetwork();
}
async function updateNetwork() { const count=(await queuedCommands().catch(()=>[])).length; const online=navigator.onLine; const element=$("#network-state");element.classList.toggle("offline",!online);element.textContent=online?(count?`再送待ち ${count}件`:"オンライン"):"オフライン"; }
window.addEventListener("online",()=>{updateNetwork();replayQueue();});window.addEventListener("offline",updateNetwork);
if ("serviceWorker" in navigator) navigator.serviceWorker.register("/service-worker.js");
updateNetwork(); showApp(Boolean(state.token)); if(state.token) replayQueue();

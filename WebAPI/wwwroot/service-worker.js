const CACHE_NAME = "home-stock-v3";
const APP_SHELL = [
  "/",
  "/index.html",
  "/app.css",
  "/dashboard.css",
  "/app.js",
  "/manifest.webmanifest",
  "/icon.svg"
];

// 基本画面を先に保存し、初回オフライン時にもアプリを起動できるようにします。
self.addEventListener("install", event => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache => cache.addAll(APP_SHELL))
  );
});

// 新しいバージョンが有効になった時点で、古いアプリ資産だけを削除します。
self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys().then(keys => Promise.all(
      keys
        .filter(key => key !== CACHE_NAME)
        .map(key => caches.delete(key))
    ))
  );
});

self.addEventListener("fetch", event => {
  const url = new URL(event.request.url);
  if (event.request.method !== "GET" || url.pathname.startsWith("/api/")) {
    return;
  }

  // 静的資産は最新レスポンスを優先し、通信できない場合のみキャッシュへ戻ります。
  event.respondWith(
    fetch(event.request)
      .then(response => {
        const copy = response.clone();
        caches.open(CACHE_NAME).then(cache => cache.put(event.request, copy));
        return response;
      })
      .catch(async () =>
        await caches.match(event.request) || await caches.match("/index.html"))
  );
});

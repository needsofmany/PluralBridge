(() => {
  const scripts = [
    "js/bootstrap.js",
    "js/api-client.js",
    "js/members.js",
    "js/developer-tools.js",
    "js/legacy-app.js"
  ];

  const loadScript = (src) => new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = src;
    script.async = false;
    script.onload = resolve;
    script.onerror = () => reject(new Error("Failed to load " + src));
    document.head.appendChild(script);
  });

  scripts.reduce((chain, src) => chain.then(() => loadScript(src)), Promise.resolve())
    .catch((error) => {
      console.error("PluralBridge browser app failed to start.", error);
    });
})();

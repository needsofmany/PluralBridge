(function () {
  function normalizePath(pathname) {
    if (!pathname || pathname === "/") {
      return "/index.html";
    }
    if (pathname.endsWith("/")) {
      return pathname + "index.html";
    }
    return pathname;
  }

  function inAny(pathname, prefixes) {
    return prefixes.some(function (prefix) {
      return pathname === prefix || pathname.startsWith(prefix);
    });
  }

  function buildNavHtml(pathname) {
    var isHome = pathname === "/index.html";
    var isStartHere = pathname === "/start-here.html";
    var isRecovery = pathname === "/export-now.html";

    var guidesCurrent = inAny(pathname, [
      "/docs.html",
      "/docs/",
      "/safety.html",
      "/developer-workflow.html",
      "/simply-plural-shutdown.html",
    ]);
    var projectsCurrent = inAny(pathname, ["/browser-app.html", "/export-now.html"]);
    var aboutCurrent = inAny(pathname, ["/about.html", "/contact.html"]);

    return (
      '<div class="header-inner">' +
      '<a class="brand" href="/index.html">' +
      '<img class="brand-logo" src="/assets/images/pluralbridge-nav-logo.png" alt="" aria-hidden="true">' +
      '<span class="brand-text">' +
      '<span class="brand-title">PluralBridge</span>' +
      '<span class="brand-subtitle">Needs of the Many</span>' +
      "</span>" +
      "</a>" +
      '<nav class="nav" aria-label="Main navigation">' +
      '<a href="/index.html"' +
      (isHome ? ' aria-current="page"' : "") +
      '><span class="nav-icon nav-icon-home" aria-hidden="true"></span>Home</a>' +
      '<a href="/start-here.html"' +
      (isStartHere ? ' aria-current="page"' : "") +
      '><span class="nav-icon nav-icon-start" aria-hidden="true"></span>Start Here</a>' +
      '<a href="/export-now.html"' +
      (isRecovery ? ' aria-current="page"' : "") +
      '><span class="nav-icon nav-icon-recovery" aria-hidden="true"></span>Data Recovery</a>' +
      '<details class="nav-menu">' +
      "<summary" +
      (guidesCurrent ? ' data-current="section"' : "") +
      '><span class="nav-icon nav-icon-guides" aria-hidden="true"></span>Guides</summary>' +
      '<div class="nav-menu-panel">' +
      '<a href="/docs.html"' +
      (pathname === "/docs.html" ? ' aria-current="page"' : "") +
      ">Documentation Home</a>" +
      '<a href="/safety.html"' +
      (pathname === "/safety.html" ? ' aria-current="page"' : "") +
      ">Safety</a>" +
      '<a href="/developer-workflow.html"' +
      (pathname === "/developer-workflow.html" ? ' aria-current="page"' : "") +
      ">Help Build</a>" +
      '<a href="/simply-plural-shutdown.html"' +
      (pathname === "/simply-plural-shutdown.html" ? ' aria-current="page"' : "") +
      ">Shutdown Info</a>" +
      "</div>" +
      "</details>" +
      '<details class="nav-menu">' +
      "<summary" +
      (projectsCurrent ? ' data-current="section"' : "") +
      '><span class="nav-icon nav-icon-projects" aria-hidden="true"></span>Projects</summary>' +
      '<div class="nav-menu-panel">' +
      '<a href="/browser-app.html"' +
      (pathname === "/browser-app.html" ? ' aria-current="page"' : "") +
      ">What PluralBridge Is Building</a>" +
      '<a href="/export-now.html"' +
      (pathname === "/export-now.html" ? ' aria-current="page"' : "") +
      ">Data Recovery Project</a>" +
      "</div>" +
      "</details>" +
      '<details class="nav-menu">' +
      '<summary><span class="nav-icon nav-icon-community" aria-hidden="true"></span>Community</summary>' +
      '<div class="nav-menu-panel">' +
      '<a href="https://github.com/needsofmany/PluralBridge/discussions">Discussions</a>' +
      '<a href="https://pluralpedia.org/w/PluralBridge">Pluralpedia</a>' +
      '<a href="https://mastodon.social/@needsofthemany">Mastodon</a>' +
      '<a class="github-link" href="https://github.com/needsofmany/PluralBridge">GitHub</a>' +
      "</div>" +
      "</details>" +
      '<details class="nav-menu">' +
      "<summary" +
      (aboutCurrent ? ' data-current="section"' : "") +
      '><span class="nav-icon nav-icon-about" aria-hidden="true"></span>About</summary>' +
      '<div class="nav-menu-panel">' +
      '<a href="/about.html"' +
      (pathname === "/about.html" ? ' aria-current="page"' : "") +
      ">About PluralBridge</a>" +
      '<a href="/contact.html"' +
      (pathname === "/contact.html" ? ' aria-current="page"' : "") +
      ">Contact Us</a>" +
      "</div>" +
      "</details>" +
      "</nav>" +
      "</div>"
    );
  }

  document.addEventListener("DOMContentLoaded", function () {
    var header = document.querySelector(".site-header");
    if (header) {
      var path = normalizePath(window.location.pathname || "");
      header.innerHTML = buildNavHtml(path);
    }

    document.querySelectorAll(".nav-menu").forEach(function (menu) {
      menu.addEventListener("toggle", function () {
        if (!menu.open) {
          return;
        }

        document.querySelectorAll(".nav-menu[open]").forEach(function (otherMenu) {
          if (otherMenu !== menu) {
            otherMenu.open = false;
          }
        });
      });
    });
  });
})();

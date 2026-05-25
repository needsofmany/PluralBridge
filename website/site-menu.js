(function () {
    document.addEventListener("DOMContentLoaded", function () {
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

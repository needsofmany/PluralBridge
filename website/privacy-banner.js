(function () {
    function setExpanded(banner, button, expanded) {
        banner.classList.toggle("is-collapsed", !expanded);
        button.setAttribute("aria-expanded", expanded ? "true" : "false");
        button.textContent = expanded ? "Hide full reminder" : "Show full reminder";
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll(".privacy-banner").forEach(function (banner) {
            var button = banner.querySelector(".privacy-banner-toggle");

            if (!button) {
                return;
            }

            setExpanded(banner, button, false);

            button.addEventListener("click", function () {
                var expanded = button.getAttribute("aria-expanded") === "true";
                setExpanded(banner, button, !expanded);
            });
        });
    });
})();

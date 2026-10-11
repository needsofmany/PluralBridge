document.addEventListener("DOMContentLoaded", () => {
  const toggles = document.querySelectorAll(".fold-toggle");

  const setState = (btn, open) => {
    btn.setAttribute("aria-expanded", String(open));
    const icon = btn.querySelector(".fold-chevron");
    const body = btn.closest(".fold-topic")?.querySelector(".fold-body");
    if (icon) icon.textContent = open ? "v" : ">";
    if (body) body.hidden = !open;
  };

  toggles.forEach((btn) => {
    setState(btn, false);
    btn.addEventListener("click", () => {
      const isOpen = btn.getAttribute("aria-expanded") === "true";
      setState(btn, !isOpen);
    });
  });
});

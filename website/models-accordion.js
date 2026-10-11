document.addEventListener("DOMContentLoaded", () => {
  const accordion = document.querySelector(".models-accordion");
  if (!accordion) return;

  const toggles = accordion.querySelectorAll(".model-toggle");

  const setOpen = (btn, open) => {
    btn.setAttribute("aria-expanded", String(open));
    const topic = btn.closest(".model-topic");
    const body = topic?.querySelector(".model-body");
    const icon = btn.querySelector(".model-chevron");
    if (body) body.hidden = !open;
    if (icon) icon.textContent = open ? "v" : ">";
  };

  toggles.forEach((btn) => {
    setOpen(btn, false);

    btn.addEventListener("click", () => {
      const isOpen = btn.getAttribute("aria-expanded") === "true";
      toggles.forEach((other) => setOpen(other, false));
      if (!isOpen) setOpen(btn, true);
    });
  });
});

document.addEventListener("DOMContentLoaded", () => {
  const toggles = document.querySelectorAll(".docs-topic-toggle");
  const storageKey = "docs-topic-state:docs-home";
  const storage = window.sessionStorage || window.localStorage;

  const readState = () => {
    try {
      const raw = storage.getItem(storageKey);
      return raw ? JSON.parse(raw) : {};
    } catch (error) {
      return {};
    }
  };

  const writeState = (state) => {
    try {
      storage.setItem(storageKey, JSON.stringify(state));
    } catch (error) {
      // Ignore storage write failures (private mode, disabled storage, etc.)
    }
  };

  const applyState = (btn, isOpen) => {
    btn.setAttribute("aria-expanded", String(isOpen));

    const body = btn.closest(".docs-topic")?.querySelector(".docs-topic-body");
    if (body) body.hidden = isOpen ? false : true;

    const chevron = btn.querySelector(".docs-topic-chevron");
    if (chevron) chevron.textContent = isOpen ? "v" : ">";
  };

  const sessionState = readState();

  toggles.forEach((btn, index) => {
    const topicId = `topic-${index}`;
    const persisted = sessionState[topicId] === true;
    applyState(btn, persisted);

    btn.addEventListener("click", () => {
      const isOpen = btn.getAttribute("aria-expanded") === "true";
      const next = isOpen ? false : true;
      applyState(btn, next);
      sessionState[topicId] = next;
      writeState(sessionState);
    });
  });
});
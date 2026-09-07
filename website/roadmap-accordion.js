(() => {
  const roadmapRoot = document.querySelector(
    ".doc-page:has(#pluralbridge-roadmap-and-post-release-task-list)"
  );
  if (!roadmapRoot) {
    return;
  }

  const phaseById = {
    "local-rest-service-boundary": "now",
    "api-contracts-and-versioning": "now",
    "service-organization-mvc-style-separation": "now",
    "contract-centered-testing": "now",
    "local-security-model": "now",
    "console-client-shell-client": "next",
    "non-technical-user-word-guide": "next",
    "sqlite-support-after-service-boundary": "next",
    "local-viewer-after-rest-path-is-usable": "next",
    "native-phone-apps-android-and-ios-ipados": "later",
    "cloud-migration-and-rest-expansion": "later",
    "platform-expansion-and-outreach": "later",
  };

  const labelByPhase = {
    now: "Now",
    next: "Next",
    later: "Later",
  };

  const topics = Array.from(roadmapRoot.querySelectorAll("h3[id]"));
  topics.forEach((topic) => {
    const phase = phaseById[topic.id];
    if (!phase) {
      return;
    }

    const label = topic.innerHTML;
    topic.classList.add("roadmap-topic");
    topic.textContent = "";

    const button = document.createElement("button");
    button.type = "button";
    button.className = "roadmap-topic-toggle";
    button.setAttribute("aria-expanded", "false");

    const chevron = document.createElement("span");
    chevron.className = "roadmap-topic-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = ">";

    const chip = document.createElement("span");
    chip.className = `roadmap-phase-chip phase-${phase}`;
    chip.textContent = labelByPhase[phase];

    const title = document.createElement("span");
    title.className = "roadmap-topic-label";
    title.innerHTML = label;

    button.append(chevron, chip, title);
    topic.appendChild(button);

    const panel = document.createElement("div");
    panel.className = "roadmap-topic-panel";
    panel.hidden = true;

    let cursor = topic.nextElementSibling;
    while (cursor && cursor.tagName !== "H3" && cursor.tagName !== "H2") {
      const next = cursor.nextElementSibling;
      panel.appendChild(cursor);
      cursor = next;
    }

    topic.insertAdjacentElement("afterend", panel);

    button.addEventListener("click", () => {
      const isOpen = button.getAttribute("aria-expanded") === "true";
      button.setAttribute("aria-expanded", isOpen ? "false" : "true");
      panel.hidden = isOpen;
      chevron.textContent = isOpen ? ">" : "v";
    });
  });
})();

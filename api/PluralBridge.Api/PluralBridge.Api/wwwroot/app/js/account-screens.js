window.PluralBridge = window.PluralBridge || {};

window.PluralBridge.accountScreens = (function () {

    const accountRoutes = {
        profile: renderProfileScreen,
        security: renderSecurityScreen,
        systems: renderSystemsScreen
    };

    function getOutput() {
        return document.getElementById("appOutput");
    }

    function createElement(tagName, className, textContent) {

        const element = document.createElement(tagName);

        if (className) {
            element.className = className;
        }

        if (textContent !== undefined && textContent !== null) {
            element.textContent = textContent;
        }

        return element;
    }

    function clearOutput() {

        const output = getOutput();

        if (!output) {
            return null;
        }

        output.replaceChildren();

        return output;
    }

    function createAccountScreen(screenKey, title, introText) {

        const output = clearOutput();

        if (!output) {
            return null;
        }

        const wrapper = createElement("section", "account-screen");
        wrapper.dataset.shellScreen = screenKey;

        wrapper.appendChild(
            createElement("h2", "output-heading", title)
        );

        wrapper.appendChild(
            createElement("p", "account-screen-intro", introText)
        );

        output.appendChild(wrapper);

        return wrapper;
    }

    function createAccountCard(title, note) {

        const card = createElement("article", "account-card");

        card.appendChild(
            createElement("h3", "account-card-title", title)
        );

        card.appendChild(
            createElement("p", "account-card-note", note)
        );

        return card;
    }

    function createAccountDetailList(rows) {

        const list = createElement("dl", "account-detail-list");

        rows.forEach(function (row) {

            const item = createElement("div", "account-detail-row");
            const label = createElement("dt", null, row.label);
            const value = createElement("dd");

            if (row.isStatus) {
                value.appendChild(
                    createElement("span", "account-status-pill", row.value)
                );
            }
            else {
                value.textContent = row.value;
            }

            item.appendChild(label);
            item.appendChild(value);
            list.appendChild(item);
        });

        return list;
    }

    function createAccountActionRow(actions) {

        const row = createElement("div", "account-action-row");

        actions.forEach(function (label) {

            const button = createElement("button", "account-secondary-button", label);
            button.type = "button";
            button.disabled = true;

            row.appendChild(button);
        });

        return row;
    }

    function appendAccountCards(wrapper, cards) {

        const grid = createElement("div", "account-card-grid");

        cards.forEach(function (card) {
            grid.appendChild(card);
        });

        wrapper.appendChild(grid);
    }

    function renderProfileScreen() {

        const wrapper =
            createAccountScreen(
                "profile",
                "Profile",
                "Account identity, display preferences, and profile-facing settings live here."
            );

        if (!wrapper) {
            return;
        }

        const accountSummary =
            createAccountCard(
                "Account summary",
                "Stable host for signed-in account profile data. Task.Account can wire this to live account data later."
            );

        accountSummary.appendChild(
            createAccountDetailList([
                {
                    label: "Display name",
                    value: "Loaded from the account profile service"
                },
                {
                    label: "Email address",
                    value: "Loaded from the signed-in account"
                },
                {
                    label: "Account status",
                    value: "Active / pending / disabled / locked",
                    isStatus: true
                },
                {
                    label: "Profile details",
                    value: "Name, pronouns, and description editing attach here"
                }
            ])
        );

        accountSummary.appendChild(
            createAccountActionRow([
                "Edit profile",
                "Refresh account"
            ])
        );

        const profilePreview =
            createAccountCard(
                "Profile preview",
                "Reserved for account-level profile presentation. This stays separate from member-profile editing."
            );

        profilePreview.appendChild(
            createAccountDetailList([
                {
                    label: "Public name",
                    value: "Profile display value"
                },
                {
                    label: "Pronouns",
                    value: "Profile pronoun value"
                },
                {
                    label: "Description",
                    value: "Profile description preview"
                }
            ])
        );

        appendAccountCards(wrapper, [
            accountSummary,
            profilePreview
        ]);
    }

    function renderSecurityScreen() {

        const wrapper =
            createAccountScreen(
                "security",
                "Security",
                "Password, verification, recovery, and session-safety features live here."
            );

        if (!wrapper) {
            return;
        }

        const passwordCard =
            createAccountCard(
                "Password and sign-in",
                "Stable host for password and sign-in controls. Existing login/logout behavior remains outside the shell."
            );

        passwordCard.appendChild(
            createAccountDetailList([
                {
                    label: "Password",
                    value: "Password-change workflow attaches here"
                },
                {
                    label: "Recovery",
                    value: "Account-recovery workflow attaches here"
                },
                {
                    label: "Last sign-in",
                    value: "Loaded from account/session services"
                }
            ])
        );

        passwordCard.appendChild(
            createAccountActionRow([
                "Change password",
                "Review recovery"
            ])
        );

        const verificationCard =
            createAccountCard(
                "Verification and access",
                "Stable host for email verification, account lock state, and active-session review."
            );

        verificationCard.appendChild(
            createAccountDetailList([
                {
                    label: "Email verification",
                    value: "Verified / pending / required",
                    isStatus: true
                },
                {
                    label: "Account lock",
                    value: "Lock state and unlock guidance attach here"
                },
                {
                    label: "Active session",
                    value: "Current browser session details attach here"
                }
            ])
        );

        verificationCard.appendChild(
            createAccountActionRow([
                "Send verification code",
                "Review sessions"
            ])
        );

        appendAccountCards(wrapper, [
            passwordCard,
            verificationCard
        ]);
    }

    function renderSystemsScreen() {

        const wrapper =
            createAccountScreen(
                "systems",
                "System(s)",
                "Account-to-system membership, ownership, and system-selection controls live here."
            );

        if (!wrapper) {
            return;
        }

        const currentSystemCard =
            createAccountCard(
                "Current system",
                "Stable host for the selected PluralBridge system and the account boundary currently in use."
            );

        currentSystemCard.appendChild(
            createAccountDetailList([
                {
                    label: "Selected system",
                    value: "Loaded from the current account context"
                },
                {
                    label: "Role",
                    value: "Owner / administrator / member / viewer",
                    isStatus: true
                },
                {
                    label: "Boundary",
                    value: "System-scoped data remains separate from account-scoped data"
                }
            ])
        );

        currentSystemCard.appendChild(
            createAccountActionRow([
                "Switch system",
                "Manage access"
            ])
        );

        const ownershipCard =
            createAccountCard(
                "System ownership",
                "Stable host for future ownership transfer, invitations, and system-level account administration."
            );

        ownershipCard.appendChild(
            createAccountDetailList([
                {
                    label: "Owned systems",
                    value: "Loaded from account/system membership services"
                },
                {
                    label: "Invitations",
                    value: "Pending system invitations attach here"
                },
                {
                    label: "Transfer controls",
                    value: "Ownership-transfer workflow attaches here"
                }
            ])
        );

        ownershipCard.appendChild(
            createAccountActionRow([
                "Create system",
                "Review invitations"
            ])
        );

        appendAccountCards(wrapper, [
            currentSystemCard,
            ownershipCard
        ]);
    }

    function render(route) {

        const renderer = accountRoutes[route];

        if (!renderer) {
            return false;
        }

        renderer();
        return true;
    }

    return {
        render: render
    };

})();

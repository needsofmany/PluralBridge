(function () {
	function initializePasswordToggles() {
		var toggleButtons = document.querySelectorAll("[data-password-toggle]");

		toggleButtons.forEach(function (button) {
			var targetId = button.getAttribute("data-toggle-target");

			if (!targetId) {
				return;
			}

			var input = document.getElementById(targetId);

			if (!input) {
				return;
			}

			button.addEventListener("click", function () {
				var showingPassword = input.type === "text";
				input.type = showingPassword ? "password" : "text";
				button.setAttribute("aria-pressed", String(!showingPassword));
				button.setAttribute(
					"aria-label",
					showingPassword ? "Show password" : "Hide password"
				);
			});
		});
	}

	function showStatus() {
		var statusElement = document.getElementById("authStatus");

		if (!statusElement) {
			return;
		}

		var query = new URLSearchParams(window.location.search);
		var page = document.body.getAttribute("data-auth-page") || "";

		if (page === "login" && query.get("error") === "1") {
			statusElement.hidden = false;
			statusElement.setAttribute("data-variant", "error");
			statusElement.textContent = "Sign in could not be completed. Check your credentials and try again.";
			return;
		}

		var status = query.get("status");
		var message = query.get("message");

		if (!status || !message) {
			return;
		}

		statusElement.hidden = false;
		statusElement.setAttribute(
			"data-variant",
			status === "success" ? "success" : "error"
		);
		statusElement.textContent = message;
	}

	initializePasswordToggles();
	showStatus();
})();

/*
 * Authentication pages.
 * Sign-in starts at the identity step. The server chooses password, verification, or registration.
 * Showing registration copies the typed identity into the form and does not create an account.
 * A valid registration submit, and an explicit resend, show the sending screen only while that request is in flight.
 * A late response is ignored after a newer request or a resume choice.
 * pluralbridge.theme accepts only the saved words light and dark. Either word wins over the device.
 * With no saved word, the page follows prefers-color-scheme and updates when that preference changes.
 * Passwords and codes stay in the fields for the current page only. They are not written to storage, the URL, or the console.
 * The remember control stores only the typed username or email under pluralbridge.rememberedIdentity. Checking it prefills a later visit and does not submit Continue. Unchecking it removes that value.
 * A newer request or a changed identity discards a late response. The server remains authoritative for acceptance, the next step, and the resend cooldown.
 * After a code is accepted, the page asks the continuation operation whether to show password entry. A page flag is not verification.
 */
(function () {
	var continuePath = "/api/account/continue";
	var registerPath = "/api/account/register";
	var verifyPath = "/api/account/verify-registration";
	var resendPath = "/api/account/resend-registration-code";
	var passwordStep = "password";
	var verificationStep = "verification";
	var registrationStep = "registration";
	var carriedRegistrationEmail = "";
	var resendCooldownSeconds = 60;
	var requestGeneration = 0;
	var cooldownTimer = 0;
	var rememberedIdentityKey = "pluralbridge.rememberedIdentity";

	// The saved value is the identity string only. A password, code, or token is never written here.
	function readRememberedIdentity() {
		try {
			return window.localStorage.getItem(rememberedIdentityKey) || "";
		} catch (error) {
			return "";
		}
	}

	function writeRememberedIdentity(value) {
		try {
			if (value) {
				window.localStorage.setItem(rememberedIdentityKey, value);
			} else {
				window.localStorage.removeItem(rememberedIdentityKey);
			}
		} catch (error) {
			return;
		}
	}

	function bindRememberedIdentity(identityInput) {
		var box = document.getElementById("rememberIdentity");

		if (!box || !identityInput) {
			return;
		}

		var stored = readRememberedIdentity();

		if (stored) {
			identityInput.value = stored;
			box.checked = true;
		}

		function persist() {
			writeRememberedIdentity(box.checked ? identityInput.value.trim() : "");
		}

		box.addEventListener("change", persist);
		identityInput.addEventListener("input", function () {
			if (box.checked) {
				persist();
			}
		});
	}

	// A saved light or dark value stays put. Any other stored value is not a PluralBridge choice.
	// When neither system query matches, stay dark. Do not treat that as a light preference.
	function bindSystemTheme() {
		var media = window.matchMedia("(prefers-color-scheme: dark)");

		function savedLabel() {
			try {
				var stored = window.localStorage.getItem("pluralbridge.theme");
				if (stored === "light" || stored === "dark") {
					return stored;
				}
				return stored ? "other" : "missing";
			} catch (error) {
				return "unavailable";
			}
		}

		function systemTheme() {
			var prefersDark = media.matches;
			var prefersLight = window.matchMedia("(prefers-color-scheme: light)").matches;
			if (prefersDark) {
				return "dark";
			}
			if (prefersLight) {
				return "light";
			}
			return "dark";
		}

		function paint(theme) {
			var color = theme === "light" ? "#ffffff" : "#101418";
			var root = document.documentElement;
			root.setAttribute("data-theme", theme);
			root.style.colorScheme = theme;
			root.style.backgroundColor = color;
			if (document.body) {
				document.body.style.backgroundColor = color;
			}
		}

		function backgroundRule(element) {
			var found = "inline:" + (element.style.backgroundColor || "none");
			for (var sheetIndex = 0; sheetIndex < document.styleSheets.length; sheetIndex++) {
				var rules;
				try {
					rules = document.styleSheets[sheetIndex].cssRules;
				} catch (error) {
					continue;
				}
				if (!rules) {
					continue;
				}
				for (var ruleIndex = 0; ruleIndex < rules.length; ruleIndex++) {
					var rule = rules[ruleIndex];
					if (!rule.selectorText || !rule.style) {
						continue;
					}
					if (!rule.style.background && !rule.style.backgroundColor) {
						continue;
					}
					try {
						if (element.matches(rule.selectorText)) {
							found = rule.selectorText + " {" + (rule.style.background || rule.style.backgroundColor) + "}";
						}
					} catch (error) {
						continue;
					}
				}
			}
			return found;
		}

		function followSystem() {
			var saved = savedLabel();
			var theme = saved === "light" || saved === "dark" ? saved : systemTheme();
			paint(theme);
			var prefersLight = window.matchMedia("(prefers-color-scheme: light)").matches;
			console.info(
				"[auth-theme] phase=applied saved=" + saved
				+ " prefersLight=" + prefersLight
				+ " prefersDark=" + media.matches
				+ " applied=" + document.documentElement.getAttribute("data-theme")
				+ " htmlBackground=" + getComputedStyle(document.documentElement).backgroundColor
				+ " bodyBackground=" + getComputedStyle(document.body).backgroundColor
				+ " htmlRule=" + backgroundRule(document.documentElement)
				+ " bodyRule=" + backgroundRule(document.body)
			);
		}

		followSystem();
		if (typeof media.addEventListener === "function") {
			media.addEventListener("change", followSystem);
		}
	}

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

	function showStatusFromQuery() {
		var statusElement = document.getElementById("authStatus");

		if (!statusElement) {
			return;
		}

		var query = new URLSearchParams(window.location.search);
		var page = document.body.getAttribute("data-auth-page") || "";

		if (page === "login" && query.get("error") === "1") {
			setStatus("Sign in could not be completed. Check your credentials and try again.", "error");
			return;
		}

		var status = query.get("status");
		var message = query.get("message");

		if (!status || !message) {
			return;
		}

		setStatus(message, status === "success" ? "success" : "error");
	}

	function setStatus(message, variant) {
		var statusElement = document.getElementById("authStatus");

		if (!statusElement) {
			return;
		}

		statusElement.hidden = false;
		statusElement.setAttribute("data-variant", variant);
		statusElement.textContent = message;
	}

	function clearStatus() {
		var statusElement = document.getElementById("authStatus");

		if (!statusElement) {
			return;
		}

		statusElement.hidden = true;
		statusElement.textContent = "";
		statusElement.removeAttribute("data-variant");
	}

	function clearSecrets() {
		var password = document.getElementById("password");
		var code = document.getElementById("verificationCode");
		var registerPassword = document.getElementById("registerPassword");

		if (password) {
			password.value = "";
		}

		if (code) {
			code.value = "";
		}

		if (registerPassword) {
			registerPassword.value = "";
		}
	}

	function showStep(name) {
		document.querySelectorAll("[data-auth-step]").forEach(function (section) {
			section.hidden = section.getAttribute("data-auth-step") !== name;
		});
	}

	function showPasswordStep(identity) {
		var code = document.getElementById("verificationCode");
		var returnField = document.getElementById("returnUrl");

		if (code) {
			code.value = "";
		}

		document.getElementById("passwordUserName").value = identity;
		document.getElementById("passwordIdentity").textContent = identity;

		if (returnField) {
			returnField.value = localReturnFromQuery();
		}

		showStep("password");
		document.getElementById("password").focus();
	}

	// Registration is the next screen for an unused identity. The form stays unsent until its own submit.
	function showRegistrationStep(identity) {
		var form = document.getElementById("registerForm");
		var username = document.getElementById("registerUsername");
		var email = document.getElementById("registerEmail");
		var displayName = document.getElementById("registerDisplayName");
		var title = document.querySelector(".auth-title");

		if (!form || !username || !email) {
			setStatus("Sign-in could not be continued.", "error");
			return;
		}

		clearSecrets();

		if (displayName) {
			displayName.value = "";
		}

		if (identity.indexOf("@") >= 0) {
			email.value = identity;
			username.value = "";
		} else {
			username.value = identity;
			email.value = "";
		}

		form.hidden = false;
		showStep(registrationStep);

		if (title && document.body.getAttribute("data-auth-page") === "login") {
			title.textContent = "Create account";
		}

		setStatus("Create the account. Nothing is created until you submit the form.", "success");

		if (username.value) {
			email.focus();
			return;
		}

		username.focus();
	}

	// The sending screen replaces the form until this request settles. It does not send a second request.
	function showSendingStep() {
		var form = document.getElementById("registerForm");
		var progress = document.getElementById("sendingProgress");
		var resume = document.getElementById("sendingResume");
		var status = document.getElementById("sendingStatus");
		var section = document.querySelector("[data-auth-step='sending']");

		if (form) {
			form.hidden = true;
		}

		if (progress) {
			progress.hidden = false;
		}

		if (resume) {
			resume.hidden = true;
		}

		if (status) {
			status.textContent = "Sending your verification email…";
		}

		if (section) {
			section.setAttribute("aria-busy", "true");
		}

		showStep("sending");
	}

	// Returns to the registration form without submitting it. Entered values stay in the fields.
	function showRegistrationForm() {
		var form = document.getElementById("registerForm");
		var submitButton = document.getElementById("registerSubmit");
		var section = document.querySelector("[data-auth-step='sending']");

		if (section) {
			section.setAttribute("aria-busy", "false");
		}

		if (form) {
			form.hidden = false;
		}

		if (submitButton) {
			submitButton.disabled = false;
		}

		if (document.querySelector("[data-auth-step='registration']")) {
			showStep("registration");
			return;
		}

		showStep("");
	}

	// The request ended without a response. The account may already exist, so this does not submit again.
	function showRegistrationUnknown(email) {
		var progress = document.getElementById("sendingProgress");
		var resume = document.getElementById("sendingResume");
		var status = document.getElementById("sendingStatus");
		var section = document.querySelector("[data-auth-step='sending']");

		carriedRegistrationEmail = email;

		if (progress) {
			progress.hidden = true;
		}

		if (resume) {
			resume.hidden = false;
		}

		if (status) {
			status.textContent = "The request did not finish. Whether the account was created is unknown.";
		}

		if (section) {
			section.setAttribute("aria-busy", "false");
		}

		showStep("sending");
	}

	function showVerificationAfterSend() {
		var section = document.querySelector("[data-auth-step='sending']");

		if (section) {
			section.setAttribute("aria-busy", "false");
		}

		showStep("verification");
	}

	// Resume leaves the unknown registration result. Neither button posts the registration form.
	function bindSendingResume() {
		var returnButton = document.getElementById("sendingReturn");
		var codeButton = document.getElementById("sendingEnterCode");

		if (!returnButton || returnButton.getAttribute("data-bound") === "true") {
			return;
		}

		returnButton.setAttribute("data-bound", "true");
		returnButton.addEventListener("click", function () {
			beginRequest();
			showRegistrationForm();
		});
		codeButton.addEventListener("click", function () {
			beginRequest();
			var emailField = document.getElementById("registerEmail");
			var email = carriedRegistrationEmail || (emailField ? emailField.value.trim() : "");
			var identity = document.getElementById("verificationIdentity");
			var signInIdentity = document.getElementById("userName");

			if (identity) {
				identity.textContent = email;
			}

			if (signInIdentity) {
				signInIdentity.value = email;
			}

			showVerificationAfterSend();
			var code = document.getElementById("verificationCode");

			if (code) {
				code.focus();
			}
		});
	}

	// Same four rules as AccountRegistrationPassword. An empty password meets none of them.
	function registrationPasswordRules(password) {
		return {
			length: password.length >= 8,
			number: /\d/.test(password),
			special: /[^A-Za-z0-9\s]/.test(password),
			edges: password.length > 0 && password === password.trim()
		};
	}

	function registrationPasswordAccepted(password) {
		var rules = registrationPasswordRules(password);
		return rules.length && rules.number && rules.special && rules.edges;
	}

	var registrationPasswordStatus = "Check the account fields. The password needs at least 8 characters, one number, one special character, and no leading or trailing whitespace.";
	var registrationPasswordEdited = false;

	// Follows the checklist after the password has been edited. Another status message is left alone.
	function syncRegistrationPasswordStatus(password) {
		var statusElement = document.getElementById("authStatus");

		if (!statusElement) {
			return;
		}

		var showingPasswordWarning = !statusElement.hidden && statusElement.textContent === registrationPasswordStatus;

		if (password.length === 0 || registrationPasswordAccepted(password)) {
			if (showingPasswordWarning) {
				clearStatus();
			}
			return;
		}

		if (!registrationPasswordEdited || (!statusElement.hidden && !showingPasswordWarning)) {
			return;
		}

		setStatus(registrationPasswordStatus, "error");
	}

	function bindPasswordRules() {
		var input = document.getElementById("registerPassword");
		var rules = document.getElementById("registerPasswordRules");
		var block = document.getElementById("registerPasswordBlock");

		if (!input || !rules || !block || block.getAttribute("data-bound") === "true") {
			return;
		}

		block.setAttribute("data-bound", "true");

		function setRule(name, met) {
			var item = rules.querySelector("[data-password-rule='" + name + "']");

			if (!item) {
				return;
			}

			item.classList.toggle("is-met", met);
		}

		function refreshPasswordRules() {
			var rules = registrationPasswordRules(input.value);

			setRule("length", rules.length);
			setRule("number", rules.number);
			setRule("special", rules.special);
			setRule("edges", rules.edges);
			syncRegistrationPasswordStatus(input.value);
		}

		input.addEventListener("input", function () {
			registrationPasswordEdited = true;
			refreshPasswordRules();
		});
		block.addEventListener("focusin", function () {
			rules.hidden = false;
			refreshPasswordRules();
		});
		block.addEventListener("focusout", function (event) {
			if (block.contains(event.relatedTarget)) {
				return;
			}

			rules.hidden = true;
		});
		refreshPasswordRules();
	}

	// Both pages share this submit. Showing the form does not call it.
	function bindRegistrationForm() {
		var form = document.getElementById("registerForm");

		if (!form || form.getAttribute("data-bound") === "true") {
			return;
		}

		form.setAttribute("data-bound", "true");
		var submitButton = document.getElementById("registerSubmit");

		form.addEventListener("submit", function (event) {
			event.preventDefault();

			if (submitButton.disabled) {
				return;
			}

			var username = document.getElementById("registerUsername").value.trim();
			var email = document.getElementById("registerEmail").value.trim();
			var displayName = document.getElementById("registerDisplayName").value.trim();
			var password = document.getElementById("registerPassword").value;

			if (!username || username.length > 100 || !email || email.length > 320 || !displayName || displayName.length > 200) {
				setStatus("Check the account fields.", "error");
				return;
			}

			if (!registrationPasswordAccepted(password)) {
				if (password.length > 0) {
					setStatus(registrationPasswordStatus, "error");
				}
				return;
			}

			var token = beginRequest();
			submitButton.disabled = true;
			showSendingStep();
			postJson(registerPath, {
				username: username,
				email: email,
				displayName: displayName,
				password: password
			}).then(function (result) {
				if (!isCurrent(token)) {
					return;
				}

				var reason = result.payload && result.payload.reasonCode;

				if (result.ok || reason === "delivery_failed") {
					carriedRegistrationEmail = email;
					document.getElementById("registerPassword").value = "";
					document.getElementById("verificationIdentity").textContent = email;
					var signInIdentity = document.getElementById("userName");

					if (signInIdentity) {
						signInIdentity.value = email;
					}

					form.hidden = true;
					showVerificationAfterSend();

					if (reason === "delivery_failed") {
						setStatus(messageFrom(result.payload, "The account was created, but the verification email could not be sent."), "error");
						startCooldown();
						return;
					}

					setStatus("Registration was accepted. Enter the verification code. This code expires in 10 minutes.", "success");
					document.getElementById("verificationCode").focus();
					return;
				}

				showRegistrationForm();
				var rejectedMessage = messageFrom(result.payload, "Registration could not be completed.");
				if (rejectedMessage !== "Registration could not be completed.") {
					setStatus(rejectedMessage, "error");
				}
			}).catch(function () {
				if (isCurrent(token)) {
					showRegistrationUnknown(email);
				}
			}).finally(function () {
				if (isCurrent(token) && !form.hidden) {
					submitButton.disabled = false;
				}
			});
		});
	}

	function followVerifiedIdentity(identity, token, unchanged, onNotPassword) {
		var code = document.getElementById("verificationCode");

		if (code) {
			code.value = "";
		}

		return postJson(continuePath, { usernameOrEmail: identity }).then(function (continued) {
			if (!isCurrent(token) || !unchanged()) {
				return;
			}

			var nextStep = continued.payload && continued.payload.nextStep;

			if (continued.ok && nextStep === passwordStep) {
				showPasswordStep(identity);
				setStatus("Enter the account password to sign in. Verification did not sign you in.", "success");
				return;
			}

			setStatus(messageFrom(continued.payload, "Sign-in could not be continued."), "error");

			if (onNotPassword) {
				onNotPassword();
			}
		});
	}

	function bindPasswordForm() {
		var passwordForm = document.getElementById("passwordForm");

		if (!passwordForm || passwordForm.getAttribute("data-bound") === "true") {
			return;
		}

		passwordForm.setAttribute("data-bound", "true");
		passwordForm.addEventListener("submit", function () {
			var identityStep = document.querySelector("[data-auth-step='identity']");
			var identityInput = document.getElementById("userName");

			if (identityStep && !identityStep.hidden && identityInput && identityInput.value.trim()) {
				document.getElementById("passwordUserName").value = identityInput.value.trim();
			}

			document.getElementById("passwordSubmit").disabled = true;
		});
	}

	function localReturnFromQuery() {
		var query = new URLSearchParams(window.location.search);
		var value = query.get("ReturnUrl") || query.get("returnUrl") || "";

		if (!value || value.charAt(0) !== "/" || value.indexOf("//") === 0 || value.indexOf("\\") >= 0 || value.indexOf("://") >= 0) {
			return "";
		}

		return value;
	}

	function beginRequest() {
		requestGeneration += 1;
		return requestGeneration;
	}

	function isCurrent(token) {
		return token === requestGeneration;
	}

	function readJson(response) {
		return response.json().catch(function () {
			return null;
		});
	}

	function postJson(path, body) {
		return fetch(path, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			credentials: "same-origin",
			body: JSON.stringify(body)
		}).then(function (response) {
			return readJson(response).then(function (payload) {
				return { ok: response.ok, status: response.status, payload: payload };
			});
		});
	}

	function stopCooldown() {
		if (cooldownTimer) {
			window.clearInterval(cooldownTimer);
			cooldownTimer = 0;
		}

		var countdown = document.getElementById("resendCountdown");
		var resend = document.getElementById("resendCode");

		if (countdown) {
			countdown.hidden = true;
			countdown.textContent = "";
		}

		if (resend) {
			resend.disabled = false;
		}
	}

	function startCooldown() {
		var remaining = resendCooldownSeconds;
		var countdown = document.getElementById("resendCountdown");
		var resend = document.getElementById("resendCode");
		var token = requestGeneration;

		stopCooldown();

		if (resend) {
			resend.disabled = true;
		}

		function paint() {
			if (!isCurrent(token) || !countdown) {
				return;
			}

			countdown.hidden = false;
			countdown.textContent = "You can request another code in " + remaining + " seconds. The server decides when a new code is allowed.";
		}

		paint();
		cooldownTimer = window.setInterval(function () {
			if (!isCurrent(token)) {
				stopCooldown();
				return;
			}

			remaining -= 1;

			if (remaining <= 0) {
				stopCooldown();
				return;
			}

			paint();
		}, 1000);
	}

	function messageFrom(payload, fallback) {
		if (payload && payload.message) {
			return payload.message;
		}

		return fallback;
	}

	function initializeSignIn() {
		var identityInput = document.getElementById("userName");
		var continueButton = document.getElementById("continueSignIn");
		var passwordForm = document.getElementById("passwordForm");
		var passwordUserName = document.getElementById("passwordUserName");
		var returnField = document.getElementById("returnUrl");
		var verifyButton = document.getElementById("verifyCode");
		var resendButton = document.getElementById("resendCode");

		if (!identityInput || !continueButton) {
			return;
		}

		bindRegistrationForm();

		if (returnField) {
			returnField.value = localReturnFromQuery();
		}

		showStep("identity");
		bindRememberedIdentity(identityInput);

		identityInput.addEventListener("keydown", function (event) {
			if (event.key === "Enter") {
				event.preventDefault();
				continueButton.click();
			}
		});

		var verificationCode = document.getElementById("verificationCode");

		if (verificationCode && verifyButton) {
			verificationCode.addEventListener("keydown", function (event) {
				if (event.key === "Enter") {
					event.preventDefault();
					verifyButton.click();
				}
			});
		}

		function acceptedIdentity() {
			return identityInput.value.trim();
		}

		function returnToIdentity() {
			beginRequest();
			stopCooldown();
			clearSecrets();
			continueButton.disabled = false;
			if (passwordUserName) {
				passwordUserName.value = "";
			}
			var title = document.querySelector(".auth-title");
			if (title) {
				title.textContent = "Sign into PluralBridge";
			}
			showStep("identity");
			identityInput.focus();
		}

		document.getElementById("changeIdentityFromPassword").addEventListener("click", returnToIdentity);
		document.getElementById("changeIdentityFromVerification").addEventListener("click", returnToIdentity);

		identityInput.addEventListener("input", function () {
			clearSecrets();
			if (passwordUserName) {
				passwordUserName.value = "";
			}
		});

		continueButton.addEventListener("click", function () {
			var identity = acceptedIdentity();

			if (!identity || continueButton.disabled) {
				setStatus("Enter a username or email.", "error");
				return;
			}

			var token = beginRequest();
			continueButton.disabled = true;
			postJson(continuePath, { usernameOrEmail: identity }).then(function (result) {
				if (!isCurrent(token) || identityInput.value.trim() !== identity) {
					return;
				}

				var nextStep = result.payload && result.payload.nextStep;

				if (result.ok && nextStep === passwordStep) {
					showPasswordStep(identity);
					setStatus("Enter the account password.", "success");
					return;
				}

				if (result.ok && nextStep === verificationStep) {
					document.getElementById("verificationIdentity").textContent = identity;
					showStep("verification");
					setStatus("Enter the verification code. This code expires in 10 minutes.", "success");
					document.getElementById("verificationCode").focus();
					return;
				}

				if (result.ok && nextStep === registrationStep) {
					showRegistrationStep(identity);
					return;
				}

				showStep("identity");
				setStatus(messageFrom(result.payload, "Sign-in could not be continued."), "error");
			}).catch(function () {
				if (!isCurrent(token)) {
					return;
				}

				setStatus("The request could not be completed. Try again.", "error");
			}).finally(function () {
				if (isCurrent(token)) {
					continueButton.disabled = false;
				}
			});
		});

		verifyButton.addEventListener("click", function () {
			var identity = acceptedIdentity();
			var code = document.getElementById("verificationCode").value.trim();

			if (!/^\d{6}$/.test(code) || verifyButton.disabled) {
				setStatus("Enter the six-digit verification code.", "error");
				return;
			}

			var token = beginRequest();
			verifyButton.disabled = true;
			postJson(verifyPath, { email: identity, code: code }).then(function (result) {
				if (!isCurrent(token) || identityInput.value.trim() !== identity) {
					return;
				}

				if (result.ok) {
					followVerifiedIdentity(identity, token, function () {
						return identityInput.value.trim() === identity;
					}, function () {
						showStep("identity");
					}).catch(function () {
						if (isCurrent(token)) {
							setStatus("The request could not be completed. Try again.", "error");
							showStep("identity");
						}
					});
					return;
				}

				setStatus(messageFrom(result.payload, "The code could not be accepted."), "error");
			}).catch(function () {
				if (isCurrent(token)) {
					setStatus("The request could not be completed. Try again.", "error");
				}
			}).finally(function () {
				if (isCurrent(token)) {
					verifyButton.disabled = false;
				}
			});
		});

		resendButton.addEventListener("click", function () {
			var identity = acceptedIdentity();

			if (!identity || resendButton.disabled) {
				return;
			}

			var token = beginRequest();
			resendButton.disabled = true;
			showSendingStep();
			postJson(resendPath, { usernameOrEmail: identity }).then(function (result) {
				if (!isCurrent(token) || identityInput.value.trim() !== identity) {
					return;
				}

				showVerificationAfterSend();
				var reason = result.payload && result.payload.reasonCode;

				if (reason === "rate_limited") {
					setStatus(messageFrom(result.payload, "A verification code was requested recently. Wait and try again."), "error");
					startCooldown();
					return;
				}

				if (reason === "delivery_failed") {
					setStatus(messageFrom(result.payload, "The verification code could not be sent. Request another code when the cooldown allows."), "error");
					startCooldown();
					return;
				}

				setStatus(messageFrom(result.payload, "If the account is waiting for verification, a new code will be sent."), result.ok ? "success" : "error");

				if (result.ok) {
					startCooldown();
				}
			}).catch(function () {
				if (isCurrent(token) && identityInput.value.trim() === identity) {
					showVerificationAfterSend();
					setStatus("The request did not finish. A replacement code may already have been sent.", "error");
				}
			}).finally(function () {
				if (isCurrent(token) && !cooldownTimer) {
					resendButton.disabled = false;
				}
			});
		});
	}

	function initializeRegistration() {
		var form = document.getElementById("registerForm");

		if (!form) {
			return;
		}

		var verifyButton = document.getElementById("verifyCode");
		var resendButton = document.getElementById("resendCode");
		var continueButton = document.getElementById("continueSignIn");
		var identityInput = document.getElementById("userName");
		bindRegistrationForm();

		function registrationIdentity() {
			return carriedRegistrationEmail || document.getElementById("registerEmail").value.trim();
		}

		verifyButton.addEventListener("click", function () {
			var identity = registrationIdentity();
			var code = document.getElementById("verificationCode").value.trim();

			if (!/^\d{6}$/.test(code) || verifyButton.disabled) {
				setStatus("Enter the six-digit verification code.", "error");
				return;
			}

			var token = beginRequest();
			verifyButton.disabled = true;
			postJson(verifyPath, { email: identity, code: code }).then(function (result) {
				if (!isCurrent(token)) {
					return;
				}

				if (result.ok) {
					followVerifiedIdentity(identity, token, function () {
						return registrationIdentity() === identity;
					}, function () {
						if (identityInput) {
							identityInput.value = identity;
						}

						form.hidden = true;
						showStep("identity");
					}).catch(function () {
						if (isCurrent(token)) {
							setStatus("The request could not be completed. Try again.", "error");

							if (identityInput) {
								identityInput.value = identity;
							}

							form.hidden = true;
							showStep("identity");
						}
					});
					return;
				}

				setStatus(messageFrom(result.payload, "The code could not be accepted."), "error");
			}).catch(function () {
				if (isCurrent(token)) {
					setStatus("The request could not be completed. Try again.", "error");
				}
			}).finally(function () {
				if (isCurrent(token)) {
					verifyButton.disabled = false;
				}
			});
		});

		document.getElementById("changeIdentityFromPassword").addEventListener("click", function () {
			beginRequest();
			stopCooldown();
			clearSecrets();
			document.getElementById("passwordUserName").value = "";

			if (identityInput) {
				identityInput.value = registrationIdentity();
				identityInput.focus();
			}

			form.hidden = true;
			showStep("identity");
		});

		continueButton.addEventListener("click", function () {
			var identity = identityInput.value.trim();

			if (!identity || continueButton.disabled) {
				setStatus("Enter a username or email.", "error");
				return;
			}

			carriedRegistrationEmail = identity;
			var token = beginRequest();
			continueButton.disabled = true;
			postJson(continuePath, { usernameOrEmail: identity }).then(function (result) {
				if (!isCurrent(token) || identityInput.value.trim() !== identity) {
					return;
				}

				var nextStep = result.payload && result.payload.nextStep;

				if (result.ok && nextStep === passwordStep) {
					showPasswordStep(identity);
					setStatus("Enter the account password.", "success");
					return;
				}

				if (result.ok && nextStep === verificationStep) {
					document.getElementById("verificationIdentity").textContent = identity;
					showStep("verification");
					setStatus("Enter the verification code. This code expires in 10 minutes.", "success");
					document.getElementById("verificationCode").focus();
					return;
				}

				if (result.ok && nextStep === registrationStep) {
					showRegistrationStep(identity);
					return;
				}

				showStep("identity");
				setStatus(messageFrom(result.payload, "Sign-in could not be continued."), "error");
			}).catch(function () {
				if (isCurrent(token)) {
					setStatus("The request could not be completed. Try again.", "error");
				}
			}).finally(function () {
				if (isCurrent(token)) {
					continueButton.disabled = false;
				}
			});
		});

		resendButton.addEventListener("click", function () {
			var identity = registrationIdentity();

			if (!identity || resendButton.disabled) {
				return;
			}

			var token = beginRequest();
			resendButton.disabled = true;
			showSendingStep();
			postJson(resendPath, { usernameOrEmail: identity }).then(function (result) {
				if (!isCurrent(token)) {
					return;
				}

				showVerificationAfterSend();
				var reason = result.payload && result.payload.reasonCode;

				if (reason === "rate_limited" || reason === "delivery_failed" || result.ok) {
					if (reason === "rate_limited" || reason === "delivery_failed") {
						setStatus(messageFrom(result.payload, "The verification code could not be sent. Request another code when the cooldown allows."), "error");
					} else {
						setStatus(messageFrom(result.payload, "If the account is waiting for verification, a new code will be sent."), "success");
					}

					startCooldown();
					return;
				}

				setStatus(messageFrom(result.payload, "The verification code could not be sent."), "error");
			}).catch(function () {
				if (isCurrent(token)) {
					showVerificationAfterSend();
					setStatus("The request did not finish. A replacement code may already have been sent.", "error");
				}
			}).finally(function () {
				if (isCurrent(token) && !cooldownTimer) {
					resendButton.disabled = false;
				}
			});
		});
	}

	bindSystemTheme();
	initializePasswordToggles();
	showStatusFromQuery();
	bindPasswordForm();
	bindSendingResume();
	bindPasswordRules();

	var page = document.body.getAttribute("data-auth-page") || "";

	if (page === "login") {
		initializeSignIn();
	}

	if (page === "register") {
		initializeRegistration();
	}

	document.documentElement.setAttribute("data-auth-ready", "1");
})();

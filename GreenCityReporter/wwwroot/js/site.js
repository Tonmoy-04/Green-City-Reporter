// Green City Reporter - Frontend Script

document.addEventListener('DOMContentLoaded', () => {
	// Persistent light/dark theme toggle. The inline head script applies the
	// preference before paint so the page does not briefly flash white.
	const themeToggle = document.getElementById('theme-toggle');
	const systemTheme = window.matchMedia('(prefers-color-scheme: dark)');
	const getTheme = () => document.documentElement.getAttribute('data-bs-theme') || 'light';
	const updateThemeToggle = () => {
		if (!themeToggle) return;
		const nextTheme = getTheme() === 'dark' ? 'light' : 'dark';
		themeToggle.setAttribute('aria-label', `Switch to ${nextTheme} mode`);
		themeToggle.setAttribute('title', `Switch to ${nextTheme} mode`);
		const label = themeToggle.querySelector('.theme-toggle-label');
		if (label) label.textContent = `${nextTheme[0].toUpperCase()}${nextTheme.slice(1)} mode`;
	};
	const applyTheme = (theme, persist = false) => {
		document.documentElement.setAttribute('data-bs-theme', theme);
		document.documentElement.style.colorScheme = theme;
		if (persist) {
			try { localStorage.setItem('gcr-theme', theme); } catch { }
		}
		updateThemeToggle();
	};

	updateThemeToggle();
	themeToggle?.addEventListener('click', () => {
		applyTheme(getTheme() === 'dark' ? 'light' : 'dark', true);
	});
	systemTheme.addEventListener?.('change', (event) => {
		try {
			if (!localStorage.getItem('gcr-theme')) applyTheme(event.matches ? 'dark' : 'light');
		} catch {
			applyTheme(event.matches ? 'dark' : 'light');
		}
	});

	// Accessible password visibility controls used by account forms.
	document.querySelectorAll('[data-password-toggle]').forEach((toggle) => {
		const input = document.querySelector(toggle.dataset.passwordToggle);
		if (!(input instanceof HTMLInputElement)) return;

		toggle.addEventListener('click', () => {
			const showPassword = input.type === 'password';
			input.type = showPassword ? 'text' : 'password';
			toggle.setAttribute('aria-pressed', String(showPassword));
			toggle.setAttribute('aria-label', showPassword ? 'Hide password' : 'Show password');
			toggle.setAttribute('title', showPassword ? 'Hide password' : 'Show password');
			const icon = toggle.querySelector('i');
			icon?.classList.toggle('fa-eye', !showPassword);
			icon?.classList.toggle('fa-eye-slash', showPassword);
		});
	});

	// Scroll Progress Bar
	const scrollProgress = document.getElementById('scroll-progress');
	if (scrollProgress) {
		window.addEventListener('scroll', () => {
			const winScroll = document.body.scrollTop || document.documentElement.scrollTop;
			const height = document.documentElement.scrollHeight - document.documentElement.clientHeight;
			const scrolled = (winScroll / height) * 100;
			scrollProgress.style.width = scrolled + '%';
		});
	}

	// Let users open detail pages from anywhere on a data row while preserving
	// the normal behavior of links, buttons, forms, and selectable controls.
	const rowInteractiveSelector = 'a, button, input, select, textarea, label, form, [contenteditable="true"], [data-row-ignore]';
	const openTableRow = (row, event) => {
		const href = row.dataset.rowHref;
		if (!href) return;
		if (event.ctrlKey || event.metaKey || event.shiftKey) {
			window.open(href, '_blank', 'noopener');
		} else {
			window.location.assign(href);
		}
	};

	document.addEventListener('click', (event) => {
		if (!(event.target instanceof Element)) return;
		const row = event.target.closest('tr[data-row-href]');
		if (!row || event.target.closest(rowInteractiveSelector)) return;
		if (window.getSelection()?.toString()) return;
		openTableRow(row, event);
	});

	document.addEventListener('keydown', (event) => {
		if (event.key !== 'Enter' || !(event.target instanceof Element)) return;
		const row = event.target.closest('tr[data-row-href]');
		if (!row || event.target !== row) return;
		event.preventDefault();
		openTableRow(row, event);
	});

	const copyText = async (text) => {
		if (navigator.clipboard && window.isSecureContext) {
			await navigator.clipboard.writeText(text);
			return;
		}

		const field = document.createElement('textarea');
		field.value = text;
		field.readOnly = true;
		field.style.position = 'fixed';
		field.style.opacity = '0';
		document.body.appendChild(field);
		field.select();
		const copied = document.execCommand('copy');
		field.remove();
		if (!copied) throw new Error('Copy command failed');
	};

	document.addEventListener('click', async (event) => {
		if (!(event.target instanceof Element)) return;
		const button = event.target.closest('[data-copy-tracking]');
		if (!button) return;

		const trackingNumber = button.dataset.copyTracking;
		if (!trackingNumber) return;
		const icon = button.querySelector('i');
		const label = button.querySelector('.gcr-copy-label');
		try {
			await copyText(trackingNumber);
			icon?.classList.replace('fa-copy', 'fa-check');
			button.classList.add('is-copied');
			button.title = 'Copied!';
			button.setAttribute('aria-label', `Copied tracking number ${trackingNumber}`);
			if (label) label.textContent = 'Copied';
			window.setTimeout(() => {
				icon?.classList.replace('fa-check', 'fa-copy');
				button.classList.remove('is-copied');
				button.title = 'Copy tracking number';
				button.setAttribute('aria-label', `Copy tracking number ${trackingNumber}`);
				if (label) label.textContent = 'Copy tracking number';
			}, 1600);
		} catch {
			button.title = 'Unable to copy';
			if (label) label.textContent = 'Unable to copy tracking number';
		}
	});

	// Chatbot Widget Handler
	const chatbot = document.getElementById('green-city-chatbot');
	if (!chatbot) {
		return;
	}

	const toggle = document.getElementById('gcr-chatbot-toggle');
	const panel = document.getElementById('gcr-chatbot-panel');
	const close = document.getElementById('gcr-chatbot-close');
	const form = document.getElementById('gcr-chatbot-form');
	const input = document.getElementById('gcr-chatbot-input');
	const send = document.getElementById('gcr-chatbot-send');
	const messages = document.getElementById('gcr-chatbot-messages');
	const chatStorageKey = 'gcr-ai-chat-history';
	const welcomeMessage = "Hello! I'm your Green City AI assistant. Ask about submitting reports, tracking statuses, or your recent submissions.";
	let chatHistory = [];

	// Render the small, predictable subset of Markdown used by AI responses.
	// Nodes are created with textContent so model output can never inject HTML.
	const appendChatInlineContent = (parent, text) => {
		const tokenPattern = /(\*\*[^*\n]+\*\*|`[^`\n]+`)/g;
		let cursor = 0;
		for (const match of text.matchAll(tokenPattern)) {
			parent.append(document.createTextNode(text.slice(cursor, match.index)));
			const token = match[0];
			const element = document.createElement(token.startsWith('**') ? 'strong' : 'code');
			element.textContent = token.startsWith('**') ? token.slice(2, -2) : token.slice(1, -1);
			parent.append(element);
			cursor = match.index + token.length;
		}
		parent.append(document.createTextNode(text.slice(cursor)));
	};

	const renderAssistantMessage = (element, text) => {
		// Some models return an entire procedure on one line. Introduce block
		// boundaries before numbered steps and bold-labelled bullet points.
		const normalized = text
			.replace(/\r\n?/g, '\n')
			.replace(/([^\n])\s+(?=\d{1,2}[.)]\s+)/g, '$1\n')
			.replace(/([^\n])\s+-\s+(?=\*\*[^*]+\*\*)/g, '$1\n- ')
			.trim();
		const fragment = document.createDocumentFragment();
		let activeList = null;

		const closeList = () => { activeList = null; };
		for (const rawLine of normalized.split('\n')) {
			const line = rawLine.trim();
			if (!line) {
				closeList();
				continue;
			}

			const ordered = line.match(/^(\d{1,2})[.)]\s+(.+)$/);
			const unordered = line.match(/^[-*]\s+(.+)$/);
			if (ordered || unordered) {
				const listTag = ordered ? 'OL' : 'UL';
				if (!activeList || activeList.tagName !== listTag) {
					activeList = document.createElement(listTag.toLowerCase());
					if (ordered && ordered[1] !== '1') activeList.start = Number(ordered[1]);
					fragment.append(activeList);
				}
				const item = document.createElement('li');
				appendChatInlineContent(item, ordered ? ordered[2] : unordered[1]);
				activeList.append(item);
				continue;
			}

			closeList();
			const heading = line.match(/^#{1,3}\s+(.+)$/);
			const block = document.createElement(heading ? 'div' : 'p');
			if (heading) block.className = 'gcr-chatbot-message-heading';
			appendChatInlineContent(block, heading ? heading[1] : line);
			fragment.append(block);
		}

		element.replaceChildren(fragment);
	};

	const renderChatMessage = (element, text, role) => {
		if (role === 'assistant') {
			renderAssistantMessage(element, text);
		} else {
			element.textContent = text;
		}
	};

	const saveChatHistory = () => {
		try {
			// Keep storage small while retaining a useful recent conversation.
			localStorage.setItem(chatStorageKey, JSON.stringify(chatHistory.slice(-40)));
		} catch { }
	};

	const loadChatHistory = () => {
		try {
			const saved = JSON.parse(localStorage.getItem(chatStorageKey) || '[]');
			if (Array.isArray(saved)) {
				chatHistory = saved.filter(item => item && (item.role === 'user' || item.role === 'assistant') && typeof item.text === 'string');
			}
		} catch { chatHistory = []; }

		if (!chatHistory.length) {
			chatHistory = [{ role: 'assistant', text: welcomeMessage }];
			saveChatHistory();
		}

		messages.replaceChildren();
		chatHistory.forEach(item => {
			const element = document.createElement('div');
			element.className = `gcr-chatbot-message gcr-chatbot-message-${item.role}`;
			renderChatMessage(element, item.text, item.role);
			messages.appendChild(element);
		});
		messages.scrollTop = messages.scrollHeight;
	};

	loadChatHistory();

	const setOpen = (isOpen) => {
		panel.hidden = !isOpen;
		toggle.setAttribute('aria-expanded', String(isOpen));
		if (isOpen) {
			input.focus();
		}
	};

	const appendMessage = (text, role) => {
		const message = document.createElement('div');
		message.className = `gcr-chatbot-message gcr-chatbot-message-${role}`;
		renderChatMessage(message, text, role);
		messages.appendChild(message);
		messages.scrollTop = messages.scrollHeight;
		if (role === 'user' || role === 'assistant') {
			chatHistory.push({ role, text });
			saveChatHistory();
		}
		return message;
	};

	toggle.addEventListener('click', () => {
		setOpen(panel.hidden);
	});

	close.addEventListener('click', () => {
		setOpen(false);
	});

	form.addEventListener('submit', async (event) => {
		event.preventDefault();

		const message = input.value.trim();
		if (!message) {
			return;
		}

		const formData = new FormData(form);
		formData.set('message', message);
		appendMessage(message, 'user');
		input.disabled = true;
		send.disabled = true;
		const loading = appendMessage('Thinking...', 'assistant');
		// Keep the visual indicator out of the saved conversation.
		chatHistory.pop();
		saveChatHistory();

		try {
			const response = await fetch('/Chat/Stream', {
				method: 'POST',
				body: formData,
				headers: {
					Accept: 'text/plain'
				}
			});
			loading.remove();

			if (!response.ok) {
				appendMessage('Please try again.', 'assistant');
				return;
			}

			const streamed = document.createElement('div');
			streamed.className = 'gcr-chatbot-message gcr-chatbot-message-assistant';
			messages.appendChild(streamed);
			const reader = response.body?.getReader();
			const decoder = new TextDecoder();
			let answer = '';
			if (reader) {
				while (true) {
					const { value, done } = await reader.read();
					if (done) break;
					answer += decoder.decode(value, { stream: true });
					renderAssistantMessage(streamed, answer);
					messages.scrollTop = messages.scrollHeight;
				}
			}
			answer += decoder.decode();
			const finalAnswer = answer.trim() || 'The assistant did not return a response.';
			renderAssistantMessage(streamed, finalAnswer);
			chatHistory.push({ role: 'assistant', text: finalAnswer });
			saveChatHistory();
			input.value = '';
		} catch {
			loading.remove();
			appendMessage('The AI assistant is temporarily unavailable. You can still use all Green City Reporter features.', 'assistant');
		} finally {
			input.disabled = false;
			send.disabled = false;
			input.focus();
		}
	});
});

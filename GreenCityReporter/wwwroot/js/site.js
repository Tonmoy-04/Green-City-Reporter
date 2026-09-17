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
			element.textContent = item.text;
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
		message.textContent = text;
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
					streamed.textContent = answer;
					messages.scrollTop = messages.scrollHeight;
				}
			}
			answer += decoder.decode();
			streamed.textContent = answer || 'The assistant did not return a response.';
			chatHistory.push({ role: 'assistant', text: streamed.textContent });
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

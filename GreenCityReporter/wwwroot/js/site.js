// Green City Reporter - Frontend Script

document.addEventListener('DOMContentLoaded', () => {
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

		try {
			const response = await fetch('/Chat/Ask', {
				method: 'POST',
				body: formData,
				headers: {
					Accept: 'application/json'
				}
			});
			const data = await response.json();
			loading.remove();

			if (!response.ok) {
				appendMessage(data.error || 'Please try again.', 'assistant');
				return;
			}

			appendMessage(data.message || 'The assistant did not return a response.', 'assistant');
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

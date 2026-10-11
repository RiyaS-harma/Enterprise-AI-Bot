(() => {
  'use strict';

  const form        = document.getElementById('chat-form');
  const input       = document.getElementById('chat-input');
  const messages    = document.getElementById('chat-messages');
  const loading     = document.getElementById('loading-indicator');
  const errorBanner = document.getElementById('error-banner');
  const btnSend     = document.getElementById('btn-send');
  const btnClear    = document.getElementById('btn-clear');
  const sidebarHistoryList = document.getElementById('sidebar-history-list');
  const sidebarHistoryEmpty = document.getElementById('sidebar-history-empty');
  let activeConversationId = null;

  if (!form || !input || !messages || !loading || !errorBanner || !btnSend || !btnClear) {
    return;
  }

  // Conversation history sent to the server for context
  const history = [];

  // ── Event Listeners ──────────────────────────────────────────

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const question = input.value.trim();
    if (!question) return;
    await sendMessage(question);
  });

  input.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      form.dispatchEvent(new Event('submit'));
    }
  });

  // Auto-resize textarea
  input.addEventListener('input', () => {
    input.style.height = 'auto';
    input.style.height = Math.min(input.scrollHeight, 120) + 'px';
  });

  btnClear.addEventListener('click', async () => {
    await startNewConversation();
  });

  if (sidebarHistoryList) {
    sidebarHistoryList.addEventListener('click', async (e) => {
      const clickedElement = e.target instanceof Element ? e.target : null;
      if (!clickedElement) {
        return;
      }

      const target = clickedElement.closest('.sidebar-history-item[data-conversation-id]');
      if (!target) {
        return;
      }

      const rawConversationId = target.getAttribute('data-conversation-id');
      const conversationId = Number.parseInt(rawConversationId ?? '', 10);
      if (!Number.isInteger(conversationId) || conversationId <= 0) {
        showError('This chat item is not linked yet. Refresh the page and try again.');
        return;
      }

      await loadConversation(conversationId);
    });
  }

  // ── Core Send Logic ──────────────────────────────────────────

  async function sendMessage(question) {
    setLoading(true);
    hideError();
    appendUserMessage(question);
    input.value = '';
    input.style.height = 'auto';

    try {
      const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

      const response = await fetch('/api/chat', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': token
        },
        body: JSON.stringify({ question, history: [...history] })
      });

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Your session has expired. Please sign in again.');
        }

        if (response.status === 403) {
          throw new Error('You do not have access to this action.');
        }

        const err = await response.json().catch(() => ({}));
        throw new Error(err.errorMessage || `Server error (${response.status})`);
      }

      const data = await response.json();

      if (data.isError) {
        showError(data.errorMessage || 'An unexpected error occurred.');
        return;
      }

      appendAssistantMessage(data.answer, data.sources || []);
      const returnedConversationId = Number.parseInt(String(data.conversationId), 10);
      if (Number.isInteger(returnedConversationId) && returnedConversationId > 0) {
        activeConversationId = returnedConversationId;
      }

      upsertSidebarConversation(question, activeConversationId);

      // Update history for multi-turn context
      history.push({ role: 'user', content: question });
      history.push({ role: 'assistant', content: data.answer });

      // Keep history bounded to last 10 turns
      if (history.length > 20) history.splice(0, 2);

    } catch (err) {
      showError(err.message || 'Failed to reach the server. Please try again.');
    } finally {
      setLoading(false);
    }
  }

  // ── DOM Helpers ──────────────────────────────────────────────

  function appendUserMessage(text) {
    const el = createMessageEl('user', escapeHtml(text), []);
    messages.appendChild(el);
    scrollToBottom();
  }

  function appendAssistantMessage(text, sources) {
    const el = createMessageEl('assistant', escapeHtml(text), sources);
    messages.appendChild(el);
    scrollToBottom();
  }

  function createMessageEl(role, text, sources) {
    const wrapper = document.createElement('div');
    wrapper.className = `message ${role}-message`;

    const avatar = document.createElement('div');
    avatar.className = 'message-avatar';
    avatar.textContent = role === 'user' ? 'You' : 'AI';

    const content = document.createElement('div');
    content.className = 'message-content';
    content.innerHTML = `<p>${text.replace(/\n/g, '<br>')}</p>`;

    if (role === 'assistant' && sources.length > 0) {
      content.appendChild(buildSourcesEl(sources));
    }

    wrapper.appendChild(avatar);
    wrapper.appendChild(content);
    return wrapper;
  }

  function buildSourcesEl(sources) {
    const section = document.createElement('div');
    section.className = 'sources-section';

    const label = document.createElement('div');
    label.className = 'sources-label';
    label.textContent = 'Sources';
    section.appendChild(label);

    sources.forEach(src => {
      const item = document.createElement('div');
      item.className = 'source-item';

      const name = escapeHtml(src.documentName);
      const page = src.pageNumber ? ` — Page ${src.pageNumber}` : '';
      const dept = src.department ? ` · ${escapeHtml(src.department)}` : '';

      item.innerHTML = `<span>${name}${page}</span><span class="source-meta">${dept}</span>`;
      section.appendChild(item);
    });

    return section;
  }

  function setLoading(on) {
    loading.hidden = !on;
    btnSend.disabled = on;
    input.disabled = on;
    if (!on) input.focus();
  }

  function showError(msg) {
    errorBanner.textContent = msg;
    errorBanner.hidden = false;
  }

  function hideError() {
    errorBanner.hidden = true;
    errorBanner.textContent = '';
  }

  function scrollToBottom() {
    messages.scrollTop = messages.scrollHeight;
  }

  function escapeHtml(str) {
    return str
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  async function loadConversation(conversationId) {
    setLoading(true);
    hideError();

    try {
      const response = await fetch(`/api/chat/history/${conversationId}`, {
        method: 'GET'
      });

      let data;
      try {
        data = await response.json();
      } catch {
        throw new Error('Unexpected server response while loading history.');
      }

      if (response.status === 401) {
        throw new Error('Your session has expired. Please sign in again.');
      }

      if (response.status === 403) {
        throw new Error('You do not have access to this chat history.');
      }

      if (!response.ok || !data.success) {
        throw new Error(data.message || 'Failed to load chat history.');
      }

      messages.innerHTML = '';
      history.length = 0;

      const turns = Array.isArray(data.messages) ? data.messages : [];
      activeConversationId = conversationId;
      if (turns.length === 0) {
        appendAssistantMessage('No conversation history found for this item.', []);
        return;
      }

      turns.forEach((turn) => {
        const role = (turn.role || '').toLowerCase();
        const content = turn.content || '';

        if (role === 'assistant') {
          appendAssistantMessage(content, []);
          history.push({ role: 'assistant', content });
          return;
        }

        appendUserMessage(content);
        history.push({ role: 'user', content });
      });

      if (history.length > 20) {
        history.splice(0, history.length - 20);
      }
    } catch (err) {
      showError(err.message || 'Failed to load chat history.');
    } finally {
      setLoading(false);
    }
  }

  async function startNewConversation() {
    setLoading(true);
    hideError();

    try {
      const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
      const response = await fetch('/api/chat/new', {
        method: 'POST',
        headers: {
          'RequestVerificationToken': token
        }
      });

      const data = await response.json().catch(() => ({}));
      if (!response.ok || !data.success) {
        throw new Error(data.message || 'Failed to start a new chat.');
      }

      const conversationId = Number.parseInt(String(data.conversationId), 10);
      activeConversationId = Number.isInteger(conversationId) && conversationId > 0 ? conversationId : null;

      history.length = 0;
      messages.innerHTML = '';
      appendAssistantMessage('Started a new chat. How can I help you?', []);
    } catch (err) {
      showError(err.message || 'Failed to start a new chat.');
    } finally {
      setLoading(false);
    }
  }

  function upsertSidebarConversation(question, conversationId) {
    if (!sidebarHistoryList) {
      return;
    }

    const parsedConversationId = Number.parseInt(String(conversationId), 10);
    if (!Number.isInteger(parsedConversationId) || parsedConversationId <= 0) {
      return;
    }

    if (sidebarHistoryEmpty) {
      sidebarHistoryEmpty.hidden = true;
    }

    const existing = sidebarHistoryList.querySelector(`.sidebar-history-item[data-conversation-id="${parsedConversationId}"]`);
    if (existing) {
      existing.textContent = question;
      const existingWrapper = existing.parentElement;
      if (existingWrapper) {
        sidebarHistoryList.prepend(existingWrapper);
      }
      return;
    }

    const item = document.createElement('button');
    item.type = 'button';
    item.className = 'sidebar-history-item';
    item.textContent = question;
    item.setAttribute('data-conversation-id', String(parsedConversationId));

    const wrapper = document.createElement('li');
    wrapper.appendChild(item);
    sidebarHistoryList.prepend(wrapper);

    while (sidebarHistoryList.children.length > 8) {
      sidebarHistoryList.removeChild(sidebarHistoryList.lastElementChild);
    }
  }

})();

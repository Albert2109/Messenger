
const CHAT_SETTINGS = {
    reconnectDelayMs: 5000,
    hubEndpoint: '/chatHub',
    apiEndpoints: {
        sendMessage: '/MessangerHome/SendMessage',
        uploadFile: '/MessangerHome/UploadFile'
    }
};

(() => {
    if (!window.chatConfig) {
        console.warn('chatConfig не знайдений – chat.js відмінено');
        return;
    }

    document.addEventListener('DOMContentLoaded', () => {
        const { currentUserId, currentChatId } = window.chatConfig;
        if (!currentChatId) {
            console.warn('Чат не обрано – пропуск підключення до SignalR');
            return;
        }

        const baseUrl = window.location.origin;
        const hubUrl = `${baseUrl}${CHAT_SETTINGS.hubEndpoint}?userId=${currentUserId}`;

        const chatHubConnection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();
        chatHubConnection.on('UserOnline', userId => {
            document
                .querySelectorAll(`.avatar[data-user-id="${userId}"]`)
                .forEach(el => el.classList.add('online'));
        });
        chatHubConnection.on('UserOffline', userId => {
            document
                .querySelectorAll(`.avatar[data-user-id="${userId}"]`)
                .forEach(el => el.classList.remove('online'));
        });

        chatHubConnection.onreconnected(connId =>
            console.log('✅ З’єднання відновлено, id:', connId)
        );
        chatHubConnection.onreconnecting(err =>
            console.warn('🔄 Підключення перервано, спроба з’єднатися знову...', err)
        );
        chatHubConnection.onclose(err =>
            console.error('❌ З’єднання закрито:', err)
        );

        (async function startConnection() {
            try {
                await chatHubConnection.start();
                console.log('🚀 Підключено до SignalR:', hubUrl);
            } catch (err) {
                console.error(`🚨 Помилка підключення, повтор через ${CHAT_SETTINGS.reconnectDelayMs} ms`, err);
                setTimeout(startConnection, CHAT_SETTINGS.reconnectDelayMs);
            }
        })();
        function linkifyText(text) {
            return (text || '').replace(
                /(https?:\/\/[^\s]+)/g,
                '<a href="$1" target="_blank">$1</a>'
            );
        }
        function renderTextMessage({ id, userId, avatarUrl, content, time, isOwn }) {
            if (id && document.querySelector(`[data-message-id="${id}"]`)) return;

            const container = document.getElementById('messages');
            const messageWrapper = document.createElement('div');
            messageWrapper.className = 'message-wrapper ' +
                (isOwn ? 'justify-content-end' : 'justify-content-start');
            messageWrapper.dataset.messageId = id;
            messageWrapper.dataset.userId = userId;

            messageWrapper.innerHTML = `
        <img src="${avatarUrl}"
             class="avatar ${isOwn ? 'ms-2' : 'me-2'}"
             data-user-id="${userId}" />
        <div class="message ${isOwn ? 'message-right' : 'message-left'}">
          ${linkifyText(content)}
          <div class="message-time">${time}</div>
        </div>
      `;
            container.appendChild(messageWrapper);
            container.scrollTop = container.scrollHeight;
        }
        function renderFileMessage({ id, userId, avatarUrl, fileUrl, fileName, time, isOwn }) {
            if (id && document.querySelector(`[data-message-id="${id}"]`)) return;

            const container = document.getElementById('messages');
            const messageWrapper = document.createElement('div');
            messageWrapper.className = 'message-wrapper ' +
                (isOwn ? 'justify-content-end' : 'justify-content-start');
            messageWrapper.dataset.messageId = id;
            messageWrapper.dataset.userId = userId;

            messageWrapper.innerHTML = `
        <img src="${avatarUrl}"
             class="avatar ${isOwn ? 'ms-2' : 'me-2'}"
             data-user-id="${userId}" />
        <div class="message ${isOwn ? 'message-right' : 'message-left'}">
          <div class="file-preview mb-2"></div>
          <div class="message-time">${time}</div>
        </div>
      `;
            const previewContainer = messageWrapper.querySelector('.file-preview');
            window.preview(fileUrl, previewContainer);

            if (!previewContainer.querySelector('img, video, audio')) {
                const downloadLink = document.createElement('a');
                downloadLink.href = fileUrl;
                downloadLink.download = fileName;
                downloadLink.textContent = fileName;
                downloadLink.className = 'preview-download btn btn-sm btn-outline-primary mt-1';
                previewContainer.appendChild(downloadLink);
            }

            container.appendChild(messageWrapper);
            container.scrollTop = container.scrollHeight;
        }
        chatHubConnection.on('ReceiveMessage', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') {
                renderTextMessage({ ...args[0], isOwn: false });
            } else {
                const [login, , avatar, text, timestamp] = args;
                renderTextMessage({
                    id: crypto.randomUUID(),
                    userId: null,
                    avatarUrl: avatar,
                    content: text,
                    time: timestamp,
                    isOwn: false
                });
            }
        });

        chatHubConnection.on('ReceivePrivateMessage', (senderId, login, avatar, text, timestamp) => {
            const isOwn = senderId === currentUserId;
            renderTextMessage({
                id: crypto.randomUUID(),
                userId: senderId,
                avatarUrl: avatar,
                content: text,
                time: timestamp,
                isOwn
            });
        });

        chatHubConnection.on('ReceiveFile', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') {
                renderFileMessage({ ...args[0], isOwn: false });
            } else {
                const [login, , avatar, url, fileName, timestamp] = args;
                renderFileMessage({
                    id: crypto.randomUUID(),
                    userId: null,
                    avatarUrl: avatar,
                    fileUrl: url,
                    fileName,
                    time: timestamp,
                    isOwn: false
                });
            }
        });

        chatHubConnection.on('ReceivePrivateFile', (senderId, url, fileName, timestamp) => {
            const isOwn = senderId === currentUserId;
            const avatarUrl = isOwn
                ? window.chatConfig.currentUserAva || '/images/default-avatar.png'
                : document.querySelector(`.avatar[data-user-id="${senderId}"]`)?.src
                || '/images/default-avatar.png';

            renderFileMessage({
                id: crypto.randomUUID(),
                userId: senderId,
                avatarUrl,
                fileUrl: url,
                fileName,
                time: timestamp,
                isOwn
            });
        });

        chatHubConnection.on('MessageDeleted', messageId => {
            const el = document.querySelector(`[data-message-id="${messageId}"]`);
            if (el) el.remove();
        });

        chatHubConnection.on('MessageEdited', (messageId, newText) => {
            const wrapper = document.querySelector(`[data-message-id="${messageId}"]`);
            if (!wrapper) return;
            const messageDiv = wrapper.querySelector('.message');
            const timeText = messageDiv.querySelector('.message-time')?.textContent || '';
            messageDiv.innerHTML = linkifyText(newText) + `<div class="message-time">${timeText}</div>`;
        });

        const sendButton = document.getElementById('sendMessageBtn');
        const messageInput = document.getElementById('messageInput');
        sendButton.addEventListener('click', async event => {
            event.preventDefault();
            const text = messageInput.value.trim();
            if (!text) return;
            try {
                await fetch(CHAT_SETTINGS.apiEndpoints.sendMessage, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: new URLSearchParams({ chatId: currentChatId, text })
                });
                messageInput.value = '';
            } catch (err) {
                console.error('❌ SendMessage помилка', err);
            }
        });

        const uploadButton = document.getElementById('sendFileBtn');
        const fileInput = document.getElementById('fileInput');
        uploadButton.addEventListener('click', async event => {
            event.preventDefault();
            if (!fileInput.files.length) return;
            const formData = new FormData();
            formData.append('chatId', currentChatId);
            formData.append('file', fileInput.files[0]);
            try {
                await fetch(CHAT_SETTINGS.apiEndpoints.uploadFile, { method: 'POST', body: formData });
                fileInput.value = '';
            } catch (err) {
                console.error('❌ UploadFile помилка', err);
            }
        });
    });
})();

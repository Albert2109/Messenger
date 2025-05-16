
const GROUP_CHAT_CONFIG = {
    retryDelayMs: 5000,
    hubPath: '/groupHub',
    endpoints: {
        sendMessage: groupId => `/Group/${groupId}/SendMessage`,
        uploadFile: groupId => `/Group/${groupId}/UploadFile`
    }
};

(() => {
    if (!window.chatConfig) {
        console.warn('chatConfig не знайдений – groupChat.js відмінено');
        return;
    }

    document.addEventListener('DOMContentLoaded', () => {
        const { currentUserId, currentGroupId } = window.chatConfig;
        if (currentGroupId == null) return;

        const baseUrl = window.location.origin;
        const groupHubUrl = `${baseUrl}${GROUP_CHAT_CONFIG.hubPath}?userId=${currentUserId}`;
        const sendMessageUrl = baseUrl + GROUP_CHAT_CONFIG.endpoints.sendMessage(currentGroupId);
        const uploadFileUrl = baseUrl + GROUP_CHAT_CONFIG.endpoints.uploadFile(currentGroupId);


        const groupHubConnection = new signalR.HubConnectionBuilder()
            .withUrl(groupHubUrl)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();
        groupHubConnection.on('UserOnline', userId =>
            document
                .querySelectorAll(`.avatar[data-user-id="${userId}"]`)
                .forEach(el => el.classList.add('online'))
        );
        groupHubConnection.on('UserOffline', userId =>
            document
                .querySelectorAll(`.avatar[data-user-id="${userId}"]`)
                .forEach(el => el.classList.remove('online'))
        );


        groupHubConnection.onreconnected(connId =>
            console.log('✅ GroupHub reconnection, id:', connId)
        );
        groupHubConnection.onreconnecting(err =>
            console.warn('🔄 GroupHub reconnecting:', err)
        );
        groupHubConnection.onclose(err =>
            console.error('❌ GroupHub closed:', err)
        );


        (async function startConnection() {
            try {
                await groupHubConnection.start();
                console.log('🚀 Connected to GroupHub:', groupHubUrl);
            } catch (err) {
                console.error(`🚨 Помилка з’єднання, повтор через ${GROUP_CHAT_CONFIG.retryDelayMs} ms`, err);
                setTimeout(startConnection, GROUP_CHAT_CONFIG.retryDelayMs);
            }
        })();

   
        function linkify(text) {
            return (text || '').replace(
                /(https?:\/\/[^\s]+)/g,
                '<a href="$1" target="_blank">$1</a>'
            );
        }

     
        function renderGroupMessage({ id, userId, avatarUrl, content, time }) {
            if (document.querySelector(`[data-message-id="${id}"]`)) return;
            const list = document.getElementById('messages');
            const wrapper = document.createElement('div');
            wrapper.className = 'message-wrapper ' +
                (userId == currentUserId ? 'justify-content-end' : 'justify-content-start');
            wrapper.dataset.messageId = id;
            wrapper.dataset.userId = userId;

            wrapper.innerHTML = `
        <img src="${avatarUrl}" 
             class="avatar ${userId == currentUserId ? 'ms-2' : 'me-2'}" 
             data-user-id="${userId}" />
        <div class="message ${userId == currentUserId ? 'message-right' : 'message-left'}">
          ${linkify(content)}
          <div class="message-time">${time}</div>
        </div>
      `;
            list.appendChild(wrapper);
            list.scrollTop = list.scrollHeight;
        }

     
        function renderGroupFile({ id, userId, avatarUrl, fileUrl, fileName, time }) {
            if (document.querySelector(`[data-message-id="${id}"]`)) return;
            const list = document.getElementById('messages');
            const wrapper = document.createElement('div');
            wrapper.className = 'message-wrapper ' +
                (userId == currentUserId ? 'justify-content-end' : 'justify-content-start');
            wrapper.dataset.messageId = id;
            wrapper.dataset.userId = userId;

            wrapper.innerHTML = `
        <img src="${avatarUrl}"
             class="avatar ${userId == currentUserId ? 'ms-2' : 'me-2'}"
             data-user-id="${userId}" />
        <div class="message ${userId == currentUserId ? 'message-right' : 'message-left'}">
          <div class="file-preview mb-2"></div>
          <div class="message-time">${time}</div>
        </div>
      `;
            const previewContainer = wrapper.querySelector('.file-preview');
            window.preview(fileUrl, previewContainer);

     
            if (!previewContainer.querySelector('img, video, audio')) {
                const link = document.createElement('a');
                link.href = fileUrl;
                link.download = fileName;
                link.textContent = fileName;
                link.className = 'preview-download btn btn-sm btn-outline-primary mt-1';
                previewContainer.appendChild(link);
            }

            list.appendChild(wrapper);
            list.scrollTop = list.scrollHeight;
        }

      
        groupHubConnection.on('ReceiveGroupMessage', (id, userId, login, email, avatar, text, timestamp) => {
            renderGroupMessage({ id, userId, avatarUrl: avatar, content: text, time: timestamp });
        });
        groupHubConnection.on('ReceiveGroupFile', (id, userId, login, email, avatar, url, fileName, timestamp) => {
            renderGroupFile({ id, userId, avatarUrl: avatar, fileUrl: url, fileName, time: timestamp });
        });
        groupHubConnection.on('GroupMessageDeleted', messageId => {
            const el = document.querySelector(`[data-message-id="${messageId}"]`);
            if (el) el.remove();
        });
        groupHubConnection.on('GroupMessageEdited', (messageId, newText) => {
            const wrapper = document.querySelector(`[data-message-id="${messageId}"]`);
            if (!wrapper) return;
            const bubble = wrapper.querySelector('.message');
            const timeText = bubble.querySelector('.message-time')?.textContent || '';
            bubble.innerHTML = linkify(newText) + `<div class="message-time">${timeText}</div>`;
        });

      
        const sendBtn = document.getElementById('sendMessageBtn');
        const messageInput = document.getElementById('messageInput');
        sendBtn.addEventListener('click', async e => {
            e.preventDefault();
            const text = messageInput.value.trim();
            if (!text) return;
            try {
                await fetch(sendMessageUrl, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: new URLSearchParams({ text })
                });
                messageInput.value = '';
            } catch (err) {
                console.error('❌ SendGroupMessage error', err);
            }
        });

      
        const uploadBtn = document.getElementById('sendFileBtn');
        const fileInput = document.getElementById('fileInput');
        uploadBtn.addEventListener('click', async e => {
            e.preventDefault();
            if (!fileInput.files.length) return;
            const formData = new FormData();
            formData.append('file', fileInput.files[0]);
            try {
                await fetch(uploadFileUrl, { method: 'POST', body: formData });
                fileInput.value = '';
            } catch (err) {
                console.error('❌ UploadGroupFile error', err);
            }
        });
    });
})();

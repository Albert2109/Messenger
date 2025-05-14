

(() => {
    if (!window.chatConfig) {
        console.warn('chatConfig not defined – chat.js aborted');
        return;
    }

    document.addEventListener('DOMContentLoaded', () => {
        const { currentUserId, currentChatId } = window.chatConfig;
        if (!currentChatId) {
            console.warn('No chat selected – SignalR connection skipped');
            return;
        }

        const origin = window.location.origin;
        const hubUrl = `${origin}/chatHub?userId=${currentUserId}`;
        console.log('Initializing SignalR with URL:', hubUrl);


        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();


        connection.on('UserOnline', uid => {
            document
                .querySelectorAll(`.avatar[data-user-id="${uid}"]`)
                .forEach(el => el.classList.add('online'));
        });
        connection.on('UserOffline', uid => {
            document
                .querySelectorAll(`.avatar[data-user-id="${uid}"]`)
                .forEach(el => el.classList.remove('online'));
        });


        connection.onclose(err => console.error('SignalR closed:', err));
        connection.onreconnecting(err => console.warn('SignalR reconnecting:', err));
        connection.onreconnected(id => console.log('SignalR reconnected, id:', id));


        (async function start() {
            try {
                await connection.start();
                console.log('SignalR connected');
            } catch (e) {
                console.error('Connection error, retry in 5s', e);
                setTimeout(start, 5000);
            }
        })();


        function makeLinks(text) {
            return text.replace(/(https?:\/\/[^\s]+)/g,
                '<a href="$1" target="_blank">$1</a>');
        }


        function appendMessage({ id, userId, login, avatar, text, timestamp, isOwn }) {
            if (id && document.querySelector(`[data-message-id="${id}"]`)) return;
            const md = document.getElementById('messages');
            const wrapper = document.createElement('div');
            wrapper.className = 'message-wrapper ' +
                (isOwn ? 'justify-content-end' : 'justify-content-start');
            if (id) wrapper.dataset.messageId = id;
            if (userId) wrapper.dataset.userId = userId;
            wrapper.dataset.hasText = 'true';
            wrapper.innerHTML = `
                <img src="${avatar}"
                     class="avatar ${isOwn ? 'ms-2' : 'me-2'}"
                     data-user-id="${userId || ''}" />
                <div class="message ${isOwn ? 'message-right' : 'message-left'}">
                    ${makeLinks(text)}
                    <div class="message-time">${timestamp}</div>
                </div>
            `;
            md.appendChild(wrapper);
            md.scrollTop = md.scrollHeight;
        }

        function appendFile({ id, userId, login, avatar, url, fileName, timestamp, isOwn }) {
            if (id && document.querySelector(`[data-message-id="${id}"]`)) return;
            const md = document.getElementById('messages');
            const wrapper = document.createElement('div');
            wrapper.className = 'message-wrapper ' +
                (isOwn ? 'justify-content-end' : 'justify-content-start');
            if (id) wrapper.dataset.messageId = id;
            if (userId) wrapper.dataset.userId = userId;
            wrapper.dataset.hasText = 'false';
            wrapper.innerHTML = `
                <img src="${avatar}"
                     class="avatar ${isOwn ? 'ms-2' : 'me-2'}"
                     data-user-id="${userId || ''}" />
                <div class="message ${isOwn ? 'message-right' : 'message-left'}">
                    <div class="file-preview mb-2"></div>
                    <div class="message-time">${timestamp}</div>
                </div>
            `;
            const previewEl = wrapper.querySelector('.file-preview');
            window.preview(url, previewEl);

            const media = previewEl.querySelector('img, video, audio, a');
            if (media) {
                switch (media.tagName) {
                    case 'IMG':
                        media.classList.add('preview-image');
                        break;
                    case 'VIDEO':
                        media.classList.add('preview-video');
                        break;
                    case 'A':
                        media.classList.add('preview-download');
                        break;
                    case 'AUDIO':
                        media.classList.add('preview-audio');
                        break;
                }
            }

            md.appendChild(wrapper);
            md.scrollTop = md.scrollHeight;
        }
        connection.on('ReceiveMessage', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') {
                appendMessage({ ...args[0], isOwn: false });
            } else {
                const [login, , avatar, text, timestamp] = args;
                appendMessage({
                    id: crypto.randomUUID(),
                    userId: null,
                    login,
                    avatar,
                    text,
                    timestamp,
                    isOwn: false
                });
            }
        });

        connection.on('ReceivePrivateMessage', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') {
                const msg = args[0];
                appendMessage({ ...msg, isOwn: msg.userId == currentUserId });
            } else {
                const [login, , avatar, text, timestamp] = args;
                appendMessage({
                    id: crypto.randomUUID(),
                    userId: currentUserId,
                    login,
                    avatar,
                    text,
                    timestamp,
                    isOwn: true
                });
            }
        });

        connection.on('ReceiveFile', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') {
                appendFile({ ...args[0], isOwn: false });
            } else {
                const [login, , avatar, url, fileName, timestamp] = args;
                appendFile({
                    id: crypto.randomUUID(),
                    userId: null,
                    login,
                    avatar,
                    url,
                    fileName,
                    timestamp,
                    isOwn: false
                });
            }
        });

        connection.on('ReceivePrivateFile', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') {
                const msg = args[0];
                appendFile({ ...msg, isOwn: msg.userId == currentUserId });
            } else {
                const [login, , avatar, url, fileName, timestamp] = args;
                appendFile({
                    id: crypto.randomUUID(),
                    userId: currentUserId,
                    login,
                    avatar,
                    url,
                    fileName,
                    timestamp,
                    isOwn: true
                });
            }
        });

        connection.on('MessageDeleted', messageId => {
            const el = document.querySelector(`[data-message-id="${messageId}"]`);
            if (el) el.remove();
        });

        connection.on('MessageEdited', (messageId, newText) => {
            const wrapper = document.querySelector(`[data-message-id="${messageId}"]`);
            if (!wrapper) return;
            const msgDiv = wrapper.querySelector('.message');
            const time = wrapper.querySelector('.message-time')?.textContent || '';
            msgDiv.innerHTML = makeLinks(newText) + `<div class="message-time">${time}</div>`;
        });
        document.getElementById('sendMessageBtn').addEventListener('click', async e => {
            e.preventDefault();
            const input = document.getElementById('messageInput');
            const text = input.value.trim();
            if (!text) return;
            try {
                await fetch(`${origin}/MessangerHome/SendMessage`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: new URLSearchParams({ chatId: currentChatId, text })
                });
                input.value = '';
            } catch (err) {
                console.error('SendMessage error', err);
            }
        });
        document.getElementById('sendFileBtn').addEventListener('click', async e => {
            e.preventDefault();
            const fileInput = document.getElementById('fileInput');
            if (!fileInput.files.length) return;
            const form = new FormData();
            form.append('chatId', currentChatId);
            form.append('file', fileInput.files[0]);
            try {
                await fetch(`${origin}/MessangerHome/UploadFile`, { method: 'POST', body: form });
                fileInput.value = '';
            } catch (err) {
                console.error('UploadFile error', err);
            }
        });
       
    });
})();
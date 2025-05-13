
if (typeof window.chatConfig === 'undefined') {
    console.warn('chatConfig not defined – chat.js aborted');
} else {
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
            return text.replace(/(https?:\/\/[^\s]+)/g, '<a href="$1" target="_blank">$1</a>');
        }

       
        function appendMessage({ id, userId, avatar, text, timestamp, isOwn }) {
            if (id && document.querySelector(`[data-message-id="${id}"]`)) return;
            const md = document.getElementById('messages');
            const wrapper = document.createElement('div');
            wrapper.className = 'message-wrapper ' + (isOwn ? 'justify-content-end' : 'justify-content-start');
            if (id) wrapper.dataset.messageId = id;
            if (userId) wrapper.dataset.userId = userId;
            wrapper.dataset.hasText = 'true';
            wrapper.innerHTML = `
                <img src="${avatar}" class="avatar ${isOwn ? 'ms-2' : 'me-2'}" data-user-id="${userId || ''}" />
                <div class="message ${isOwn ? 'message-right' : 'message-left'}">
                    ${makeLinks(text)}
                    <div class="message-time">${timestamp}</div>
                </div>
            `;
            md.appendChild(wrapper);
            md.scrollTop = md.scrollHeight;
        }

       
        function appendFile({ id, userId, avatar, url, fileName, timestamp, isOwn }) {
            if (id && document.querySelector(`[data-message-id="${id}"]`)) return;
            const md = document.getElementById('messages');
            const wrapper = document.createElement('div');
            wrapper.className = 'message-wrapper ' + (isOwn ? 'justify-content-end' : 'justify-content-start');
            if (id) wrapper.dataset.messageId = id;
            if (userId) wrapper.dataset.userId = userId;
            wrapper.dataset.hasText = 'false';
            wrapper.innerHTML = `
                <img src="${avatar}" class="avatar ${isOwn ? 'ms-2' : 'me-2'}" data-user-id="${userId || ''}" />
                <div class="message ${isOwn ? 'message-right' : 'message-left'}">
                    <div class="file-preview mb-2"></div>
                    <div class="message-time">${timestamp}</div>
                </div>
            `;
            const previewEl = wrapper.querySelector('.file-preview');
            window.preview(url, previewEl);
          
            const media = previewEl.querySelector('img, video, audio, a');
            if (media) {
                
                if (media.tagName === 'IMG') media.classList.add('preview-image');
                else if (media.tagName === 'VIDEO') media.classList.add('preview-video');
                else if (media.tagName === 'A' && media.hasAttribute('download') === false) media.classList.add('preview-download');
                else if (media.tagName === 'A' && media.hasAttribute('download')) media.classList.add('preview-download');
                else if (media.tagName === 'AUDIO') media.classList.add('preview-audio');
            }
            
            const dlBtn = previewEl.querySelector('a[download]');
            if (dlBtn) dlBtn.classList.add('preview-download');
            md.appendChild(wrapper);
            md.scrollTop = md.scrollHeight;
        }

        
        connection.on('ReceiveMessage', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') appendMessage({ ...args[0], isOwn: false });
            else {
                const [login, , avatar, text, timestamp] = args;
                appendMessage({ id: `${login}-${timestamp}`, userId: null, avatar, text, timestamp, isOwn: false });
            }
        });
        connection.on('ReceivePrivateMessage', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') appendMessage({ ...args[0], isOwn: args[0].userId == currentUserId });
            else {
                const [login, , avatar, text, timestamp] = args;
                appendMessage({ id: `${login}-${timestamp}`, userId: currentUserId, avatar, text, timestamp, isOwn: true });
            }
        });
        connection.on('ReceiveFile', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') appendFile({ ...args[0], isOwn: false });
            else {
                const [login, , avatar, url, fileName, timestamp] = args;
                appendFile({ id: `${url}-${timestamp}`, userId: null, avatar, url, fileName, timestamp, isOwn: false });
            }
        });
        connection.on('ReceivePrivateFile', (...args) => {
            if (args.length === 1 && typeof args[0] === 'object') appendFile({ ...args[0], isOwn: args[0].userId == currentUserId });
            else {
                const [login, , avatar, url, fileName, timestamp] = args;
                appendFile({ id: `${url}-${timestamp}`, userId: currentUserId, avatar, url, fileName, timestamp, isOwn: true });
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

        document.getElementById('userSearch').addEventListener('input', e => {
            const q = e.target.value.trim();
            const list = document.getElementById('searchResults');
            list.innerHTML = '';
            if (q.length < 2) return;
            fetch(`${origin}/Account/Search?q=${encodeURIComponent(q)}`)
                .then(r => r.json())
                .then(users => users.forEach(u => {
                    const li = document.createElement('li');
                    li.className = 'list-group-item list-group-item-action';
                    li.textContent = `${u.login} (${u.id})`;
                    li.onclick = () => window.location.search = `?chatId=${u.id}`;
                    list.appendChild(li);
                }));
        });
    });
}
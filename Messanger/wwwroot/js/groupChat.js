
(() => {
    if (!window.chatConfig) {
        console.warn('chatConfig not defined – groupChat.js aborted');
        return;
    }

    document.addEventListener('DOMContentLoaded', () => {
        const { currentUserId, currentGroupId } = window.chatConfig;
        if (currentGroupId == null) return;

        const origin = window.location.origin;
        const hubUrl = `${origin}/groupHub?userId=${currentUserId}`;
        const sendTextUrl = `${origin}/Group/${currentGroupId}/SendMessage`;
        const sendFileUrl = `${origin}/Group/${currentGroupId}/UploadFile`;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

       
        connection.on('UserOnline', uid => document
            .querySelectorAll(`.avatar[data-user-id="${uid}"]`)
            .forEach(el => el.classList.add('online'))
        );
        connection.on('UserOffline', uid => document
            .querySelectorAll(`.avatar[data-user-id="${uid}"]`)
            .forEach(el => el.classList.remove('online'))
        );

        connection.onreconnected(id => console.log('✅ GroupHub reconnected', id));
        connection.onreconnecting(err => console.warn('🔄 GroupHub reconnecting', err));
        connection.onclose(err => console.error('❌ GroupHub closed', err));

        
        (async function start() {
            try {
                await connection.start();
                console.log('🚀 Connected to GroupHub:', hubUrl);
            } catch (e) {
                console.error('🚨 Connection failed, retry in 5s', e);
                setTimeout(start, 5000);
            }
        })();

      
        function makeLinks(raw) {
            const text = raw == null ? '' : String(raw);
            return text.replace(
                /(https?:\/\/[^\s]+)/g,
                '<a href="$1" target="_blank">$1</a>'
            );
        }

       
        function appendGroupMessage({ id, userId, avatar, text, timestamp }) {
            if (document.querySelector(`[data-message-id="${id}"]`)) return;
            const container = document.getElementById('messages');

            const wrap = document.createElement('div');
            wrap.className = 'message-wrapper ' +
                (userId == currentUserId ? 'justify-content-end' : 'justify-content-start');
            wrap.dataset.messageId = id;
            wrap.dataset.userId = userId;

            const img = document.createElement('img');
            img.src = avatar;
            img.className = 'avatar ' + (userId == currentUserId ? 'ms-2' : 'me-2');
            img.dataset.userId = userId;
            wrap.appendChild(img);

            const bubble = document.createElement('div');
            bubble.className = 'message ' +
                (userId == currentUserId ? 'message-right' : 'message-left');
            bubble.innerHTML = makeLinks(text);
            wrap.appendChild(bubble);

            const timeEl = document.createElement('div');
            timeEl.className = 'message-time';
            timeEl.textContent = timestamp;
            bubble.appendChild(timeEl);

            container.appendChild(wrap);
            container.scrollTop = container.scrollHeight;
        }

       
        function appendGroupFile({ id, userId, avatar, url, fileName, timestamp }) {
            if (document.querySelector(`[data-message-id="${id}"]`)) return;
            const container = document.getElementById('messages');

            const wrap = document.createElement('div');
            wrap.className = 'message-wrapper ' +
                (userId == currentUserId ? 'justify-content-end' : 'justify-content-start');
            wrap.dataset.messageId = id;
            wrap.dataset.userId = userId;

            const img = document.createElement('img');
            img.src = avatar;
            img.className = 'avatar ' + (userId == currentUserId ? 'ms-2' : 'me-2');
            img.dataset.userId = userId;
            wrap.appendChild(img);

            const bubble = document.createElement('div');
            bubble.className = 'message ' +
                (userId == currentUserId ? 'message-right' : 'message-left');
            wrap.appendChild(bubble);

         
            const preview = document.createElement('div');
            preview.className = 'file-preview mb-2';
            bubble.appendChild(preview);

            
            const timeEl = document.createElement('div');
            timeEl.className = 'message-time';
            timeEl.textContent = timestamp;
            bubble.appendChild(timeEl);

          
            window.preview(url, preview);

            
            const dl = document.createElement('a');
            dl.href = url;
            dl.download = fileName;
            dl.textContent = fileName;
            dl.className = 'preview-download btn btn-sm btn-outline-primary mt-1';
            preview.appendChild(dl);

            container.appendChild(wrap);
            container.scrollTop = container.scrollHeight;
        }

       
        connection.on('ReceiveGroupMessage', (id, userId, login, email, avatar, text, timestamp) => {
            appendGroupMessage({ id, userId, avatar, text, timestamp });
        });
        connection.on('ReceiveGroupFile', (id, userId, login, email, avatar, url, fileName, timestamp) => {
            appendGroupFile({ id, userId, avatar, url, fileName, timestamp });
        });
        connection.on('GroupMessageDeleted', messageId => {
            const el = document.querySelector(`[data-message-id="${messageId}"]`);
            if (el) el.remove();
        });
        connection.on('GroupMessageEdited', (messageId, newText) => {
            const wrap = document.querySelector(`[data-message-id="${messageId}"]`);
            if (!wrap) return;
            const bubble = wrap.querySelector('.message');
            const time = bubble.querySelector('.message-time')?.textContent || '';
            bubble.innerHTML = makeLinks(newText);
            const timeEl = document.createElement('div');
            timeEl.className = 'message-time';
            timeEl.textContent = time;
            bubble.appendChild(timeEl);
        });

       
        document.getElementById('sendMessageBtn').addEventListener('click', async e => {
            e.preventDefault();
            const input = document.getElementById('messageInput');
            const text = input.value.trim();
            if (!text) return;
            try {
                await fetch(sendTextUrl, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: new URLSearchParams({ text })
                });
                input.value = '';
            } catch (err) {
                console.error('❌ SendGroupMessage error', err);
            }
        });

    
        document.getElementById('sendFileBtn').addEventListener('click', async e => {
            e.preventDefault();
            const fi = document.getElementById('fileInput');
            if (!fi.files.length) return;
            const form = new FormData();
            form.append('file', fi.files[0]);
            try {
                await fetch(sendFileUrl, { method: 'POST', body: form });
                fi.value = '';
            } catch (err) {
                console.error('❌ UploadGroupFile error', err);
            }
        });
    });
})();

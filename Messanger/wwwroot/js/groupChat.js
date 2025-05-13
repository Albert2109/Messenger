

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

        
        function makeLinks(text) {
            return text.replace(/(https?:\/\/[^\s]+)/g,
                '<a href="$1" target="_blank">$1</a>');
        }

        
        function appendGroupMessage({ id, userId, avatar, text, timestamp }) {
            if (document.querySelector(`[data-message-id="${id}"]`)) return;
            const md = document.getElementById('messages');
            const wrap = document.createElement('div');
            wrap.className = 'message-wrapper ' + (userId == currentUserId ? 'justify-content-end' : 'justify-content-start');
            wrap.dataset.messageId = id;
            wrap.dataset.userId = userId;

            wrap.innerHTML = `
        <img src="${avatar}" 
             class="avatar ${userId == currentUserId ? 'ms-2' : 'me-2'}" 
             data-user-id="${userId}" />
        <div class="message ${userId == currentUserId ? 'message-right' : 'message-left'}">
          ${makeLinks(text)}
          <div class="message-time">${timestamp}</div>
        </div>
      `;

            md.appendChild(wrap);
            md.scrollTop = md.scrollHeight;
        }

        function appendGroupFile({ id, userId, avatar, url, fileName, timestamp }) {
            if (document.querySelector(`[data-message-id="${id}"]`)) return;
            const md = document.getElementById('messages');
            const wrap = document.createElement('div');
            wrap.className = 'message-wrapper ' + (userId == currentUserId ? 'justify-content-end' : 'justify-content-start');
            wrap.dataset.messageId = id;
            wrap.dataset.userId = userId;

            wrap.innerHTML = `
        <img src="${avatar}" 
             class="avatar ${userId == currentUserId ? 'ms-2' : 'me-2'}" 
             data-user-id="${userId}" />
        <div class="message ${userId == currentUserId ? 'message-right' : 'message-left'}">
          <div class="file-preview"></div>
          <div><small class="text-muted">${timestamp}</small></div>
        </div>
      `;
            const preview = wrap.querySelector('.file-preview');
            window.preview(url, preview);

            const dl = document.createElement('a');
            dl.href = url;
            dl.download = fileName;
            dl.textContent = fileName;
            dl.className = 'btn btn-sm btn-outline-primary mt-2';
            preview.appendChild(dl);

            md.appendChild(wrap);
            md.scrollTop = md.scrollHeight;
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
            const msgDiv = wrap.querySelector('.message');
            const time = msgDiv.querySelector('.message-time')?.textContent || '';
            msgDiv.innerHTML = makeLinks(newText) + `<div class="message-time">${time}</div>`;
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


console.log('chat.js v2.2.0 loaded');
console.log('signalR global:', typeof signalR, signalR);
document.addEventListener('DOMContentLoaded', () => {
    const {
        currentUserId,
        currentUserLogin,
        currentUserAva,
        currentChatId
    } = window.chatConfig;
    console.log('chatConfig:', window.chatConfig);
    if (!currentChatId) {
        console.warn('Chat not selected — SignalR connection skipped');
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
    connection.onclose(error => console.error('SignalR connection closed:', error));
    connection.onreconnecting(error => console.warn('SignalR reconnecting:', error));
    connection.onreconnected(connectionId => console.log('SignalR reconnected, connectionId:', connectionId));
    async function start() {
        try {
            await connection.start();
            console.log('SignalR connected to', hubUrl);
        } catch (err) {
            console.error('Connection error, retrying in 5s...', err);
            setTimeout(start, 5000);
        }
    }
    start();
    connection.on('ReceiveMessage', (login, email, avatar, text, timestamp) => {
        console.log('ReceiveMessage event:', login, text, timestamp);
        appendMessage(login, avatar, text, false, timestamp);
    });
    connection.on('ReceivePrivateMessage', (login, email, avatar, text, timestamp) => {
        console.log('ReceivePrivateMessage event:', login, text, timestamp);
        appendMessage(`${login} (private)`, avatar, text, true, timestamp);
    });
    connection.on('ReceiveFile', (login, email, avatar, url, fileName, timestamp) => {
        console.log('ReceiveFile event:', fileName, url, timestamp);
        appendFile(login, avatar, url, fileName, false, timestamp);
    });
    connection.on('ReceivePrivateFile', (login, email, avatar, url, fileName, timestamp) => {
        console.log('ReceivePrivateFile event:', fileName, url, timestamp);
        appendFile(login, avatar, url, fileName, true, timestamp);
    });
   
    connection.on('MessageDeleted', messageId => {
        console.log('MessageDeleted:', messageId);
        
        const wrapper = document.querySelector(`[data-message-id="${messageId}"]`);
        if (wrapper) {
            
            wrapper.remove();
            
        }
    });

    
    connection.on('MessageEdited', (messageId, newText) => {
        console.log('MessageEdited:', messageId, newText);
        const wrapper = document.querySelector(`[data-message-id="${messageId}"]`);
        if (!wrapper) return;
       
        const msgDiv = wrapper.querySelector('.message');
        if (msgDiv) {
            
            const linked = newText.replace(/(https?:\/\/[^\s]+)/g, '<a href="$1" target="_blank">$1</a>');
            msgDiv.innerHTML = linked + `<div class="message-time">${wrapper.querySelector('.message-time').textContent}</div>`;
        }
    });

    function makeLinks(text) {
        return text.replace(/(https?:\/\/[^\s]+)/g, '<a href="$1" target="_blank">$1</a>');
    }
    function appendMessage(name, avatar, text, highlight, time) {
        const md = document.getElementById('messages');
        const d = document.createElement('div');
        d.className = 'mb-2 d-flex align-items-start' +
            (highlight ? ' bg-warning bg-opacity-10 rounded p-1' : '');
        d.innerHTML = `
      <img src="${avatar}" class="avatar me-2" />
      <div>
        <strong>${name}:</strong><br/>${makeLinks(text)}
        <br/><small class="text-muted">${time}</small>
      </div>`;
        md.appendChild(d);
        md.scrollTop = md.scrollHeight;
    }
    function appendFile(name, avatar, url, fileName, highlight, time) {
        const md = document.getElementById('messages');
        const wrapper = document.createElement('div');
        wrapper.className = 'mb-2 d-flex align-items-start' +
            (highlight ? ' bg-warning bg-opacity-10 rounded p-1' : '');
        wrapper.innerHTML = `
      <img src="${avatar}" class="avatar me-2" />
      <div class="flex-grow-1">
        <strong>${name}:</strong>
        <div class="file-preview mt-1"></div>
        <br/><small class="text-muted">${time}</small>
      </div>`;
        const previewContainer = wrapper.querySelector('.file-preview');
        window.preview(url, previewContainer);
        const dl = document.createElement('a');
        dl.href = url;
        dl.download = fileName;
        dl.textContent = 'Завантажити';
        dl.className = 'btn btn-sm btn-outline-primary mt-2';
        previewContainer.parentElement.appendChild(dl);

        md.appendChild(wrapper);
        md.scrollTop = md.scrollHeight;
    }
    document.getElementById('sendMessageBtn').addEventListener('click', async e => {
        e.preventDefault();
        const input = document.getElementById('messageInput');
        const text = input.value.trim();
        if (!text) return;

        try {
            console.log('Sending message via HTTP POST:', text);
            const res = await fetch(`${origin}/MessangerHome/SendMessage`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({ chatId: currentChatId, text })
            });
            if (!res.ok) throw new Error(await res.text());
            input.value = '';
        } catch (err) {
            console.error('SendMessage error:', err);
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
            console.log('Uploading file via HTTP POST:', fileInput.files[0].name);
            const res = await fetch(`${origin}/MessangerHome/UploadFile`, {
                method: 'POST',
                body: form
            });
            if (!res.ok) throw new Error(await res.text());
            fileInput.value = '';
        } catch (err) {
            console.error('UploadFile error:', err);
        }
    });
    document.getElementById('userSearch').addEventListener('input', function (e) {
        const q = e.target.value.trim();
        const list = document.getElementById('searchResults');
        list.innerHTML = '';
        if (q.length < 2) return;
        fetch(`${origin}/Chat/SearchUsers?query=${encodeURIComponent(q)}`)
            .then(r => r.json())
            .then(users => users.forEach(u => {
                const li = document.createElement('li');
                li.className = 'list-group-item list-group-item-action';
                li.textContent = `${u.userName} (${u.email})`;
                li.onclick = () => window.location = `?chatId=${u.id}`;
                list.appendChild(li);
            }));
    });
});

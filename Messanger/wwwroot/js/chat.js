const {
    currentUserId,
    currentUserLogin,
    currentUserAva,
    currentChatId
} = window.chatConfig;

const connection = new signalR.HubConnectionBuilder()
    .withUrl('/chatHub?userId=' + currentUserId)
    .withAutomaticReconnect()
    .build();

function makeLinks(text) {
    return text.replace(/(https?:\/\/[^\s]+)/g, '<a href="$1" target="_blank">$1</a>');
}

connection.on('ReceiveMessage', function (name, email, avatar, text) {
    appendMessage(name, avatar, text, false);
});

connection.on('ReceivePrivateMessage', function (name, email, avatar, text) {
    appendMessage(name + ' (private)', avatar, text, true);
});

connection.on('ReceivePrivateFile', function (name, email, avatar, url, file) {
    appendFile(name, avatar, url, file, false);
});

function appendMessage(name, avatar, text, highlight) {
    const md = document.getElementById('messages');
    const d = document.createElement('div');
    d.className = 'mb-2 d-flex align-items-start' + (highlight ? ' bg-warning bg-opacity-10 rounded p-1' : '');
    d.innerHTML = `
        <img src="${avatar}" class="avatar me-2" />
        <div>
            <strong>${name}:</strong><br/>${makeLinks(text)}
            <br/><small class="text-muted">${new Date().toLocaleTimeString()}</small>
        </div>`;
    md.appendChild(d);
    md.scrollTop = md.scrollHeight;
}

 function appendFile(name, avatar, url, fileName, highlight) {
         const container = document.getElementById('messages');
         const wrapper = document.createElement('div');
         wrapper.className = 'mb-2 d-flex align-items-start' + (highlight ? ' bg-warning bg-opacity-10 rounded p-1' : '');
    
        
             wrapper.innerHTML = `
       <img src="${avatar}" class="avatar me-2" />
       <div class="flex-grow-1">
            <strong>${name}:</strong>
        </div>`;
    
             const content = wrapper.querySelector('.flex-grow-1');
    
           
             const mediaContainer = document.createElement('div');
        mediaContainer.className = 'mt-1';
        window.preview(url, mediaContainer);
         content.appendChild(mediaContainer);
    
             
             const dl = document.createElement('a');
         dl.href = url;
         dl.download = fileName;
        dl.textContent = 'Завантажити';
         dl.className = 'btn btn-sm btn-outline-primary mt-2';
         content.appendChild(dl);
    
            
            const time = document.createElement('small');
      time.className = 'text-muted d-block mt-1';
       time.textContent = new Date().toLocaleTimeString();
       content.appendChild(time);
    
         
            container.appendChild(wrapper);
       container.scrollTop = container.scrollHeight;
    }

document.getElementById('sendFileBtn').addEventListener('click', async e => {
    e.preventDefault();
    if (currentChatId === null) return;

    const input = document.getElementById('fileInput');
    if (!input.files.length) return;

    const file = input.files[0];
    const form = new FormData();
    form.append('file', file);

    const res = await fetch(`/MessangerHome/UploadFile?chatId=${currentChatId}`, {
        method: 'POST',
        body: form
    });

    if (res.ok) {
        const { downloadUrl, fileName } = await res.json();
        appendFile(currentUserLogin, currentUserAva, downloadUrl, fileName);
    } else {
        console.error('Upload failed', await res.text());
    }

    input.value = '';
});

document.getElementById('userSearch').addEventListener('input', function (e) {
    const q = e.target.value.trim();
    const list = document.getElementById('searchResults');
    list.innerHTML = '';
    if (q.length < 2) return;

    fetch(`/Chat/SearchUsers?query=${encodeURIComponent(q)}`)
        .then(r => r.json())
        .then(users => users.forEach(u => {
            const li = document.createElement('li');
            li.className = 'list-group-item list-group-item-action';
            li.textContent = `${u.userName} (${u.email})`;
            li.onclick = () => { window.location = '?chatId=' + u.id; };
            list.appendChild(li);
        }));
});

connection.start().catch(err => console.error(err));

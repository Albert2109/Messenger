
const profileConnection = new signalR.HubConnectionBuilder()
    .withUrl("/profileHub")
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();
profileConnection.on("ProfileUpdated", (userId, newLogin, newEmail, newAva) => {
    document.querySelectorAll(`.avatar[data-user-id="${userId}"]`)
        .forEach(img => img.src = newAva);
    document.querySelectorAll(`.list-group-item[data-user-id="${userId}"] span`)
        .forEach(span => span.textContent = newLogin);
    if (window.chatConfig.currentChatId == userId) {
        const header = document.querySelector('.chat-container h5');
        if (header) header.textContent = `Чат з ${newLogin}`;
    }
});
async function startProfileHub() {
    try {
        await profileConnection.start();
        console.log("Connected to ProfileHub");
    } catch (err) {
        console.warn("ProfileHub reconnect in 5s", err);
        setTimeout(startProfileHub, 5000);
    }
}
startProfileHub();
document.addEventListener('click', e => {
    const avatar = e.target.closest('img.clickable-avatar');
    if (!avatar) return;
    const userId = avatar.dataset.userId;
    if (!userId) return;

    const isOwn = parseInt(userId, 10) === parseInt(window.chatConfig.currentUserId, 10);

    fetch(`/Account/GetProfile?id=${userId}`)
        .then(r => {
            if (!r.ok) throw new Error('Profile not found');
            return r.json();
        })
        .then(u => {
            document.getElementById('profileUserId').value = u.userId;
            document.getElementById('profileLogin').value = u.login;
            document.getElementById('profileEmail').value = u.email;
            document.getElementById('profileAvaPreview').src = u.ava;
            document.getElementById('profileLogin').readOnly = !isOwn;
            document.getElementById('profileEmail').readOnly = !isOwn;
            const fileInput = document.querySelector('#profileForm input[name="AvaFile"]');
            fileInput.style.display = isOwn ? '' : 'none';
            const saveBtn = document.querySelector('#profileForm button[type="submit"]');
            saveBtn.style.display = isOwn ? '' : 'none';
            const title = document.querySelector('#profileModal .modal-title');
            title.textContent = isOwn ? 'Мій профіль' : 'Перегляд профілю';
            new bootstrap.Modal(document.getElementById('profileModal')).show();
        })
        .catch(err => console.error(err));
});
const form = document.getElementById('profileForm');
if (form) {
    form.addEventListener('submit', async e => {
        e.preventDefault();
        const userId = parseInt(document.getElementById('profileUserId').value, 10);
        if (userId !== parseInt(window.chatConfig.currentUserId, 10)) return; 

        const fm = new FormData(e.target);
        const res = await fetch('/Account/UpdateProfile', {
            method: 'POST',
            body: fm
        });
        if (res.ok) {
            bootstrap.Modal.getInstance(document.getElementById('profileModal')).hide();
        }
    });
}
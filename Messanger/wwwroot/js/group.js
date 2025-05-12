// wwwroot/js/group.js
// -------------------------------------------------
// Клієнтська логіка для групових чатів
// -------------------------------------------------
/* global bootstrap, signalR, window */

//--------------------------------------------------
// 0) Підключення до GroupHub
//--------------------------------------------------
const groupHub = new signalR.HubConnectionBuilder()
    .withUrl(`/groupHub?userId=${window.chatConfig.currentUserId}`)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

async function startGroupHub() {
    try {
        await groupHub.start();
        console.log('Connected to GroupHub');
    } catch (e) {
        console.warn('GroupHub reconnect in 5s', e);
        setTimeout(startGroupHub, 5000);
    }
}
startGroupHub();

//--------------------------------------------------
// 1) Модалка «Створити групу»
//--------------------------------------------------
const btnNewGroup = document.getElementById('btnNewGroup');
const newGroupModal = new bootstrap.Modal('#newGroupModal');
btnNewGroup?.addEventListener('click', () => newGroupModal.show());

document.getElementById('newGroupForm')?.addEventListener('submit', async e => {
    e.preventDefault();
    const fm = new FormData(e.target);
    const ids = document.getElementById('memberIds').value
        .split(',').map(s => s.trim()).filter(Boolean);
    ids.forEach(id => fm.append('memberIds', id));

    const res = await fetch('/Group/Create', { method: 'POST', body: fm });
    if (res.ok) {
        newGroupModal.hide();
        location.reload();
    } else {
        alert('Не вдалося створити групу');
    }
});

//--------------------------------------------------
// 2) Обгортки для запитів до API GroupController
//--------------------------------------------------
async function post(url, payload) {
    const fm = new FormData();
    if (payload) Object.entries(payload).forEach(([k, v]) => fm.append(k, v));
    const res = await fetch(url, { method: 'POST', body: fm });
    if (!res.ok) throw new Error(await res.text());
}

export const GroupApi = {
    addMember: (g, u) => post(`/Group/${g}/AddMember`, { userId: u }),
    removeMember: (g, u) => post(`/Group/${g}/RemoveMember`, { userId: u }),
    rename: (g, n) => post(`/Group/${g}/Rename`, { name: n }),
    transferOwner: (g, o) => post(`/Group/${g}/TransferOwner`, { newOwnerId: o }),
    leave: g => post(`/Group/${g}/Leave`),
    delete: g => post(`/Group/${g}/Delete`),
    changeAvatar: async (g, file) => {
        const fm = new FormData();
        fm.append('file', file);
        await fetch(`/Group/${g}/Avatar`, { method: 'POST', body: fm });
    }
};

//--------------------------------------------------
// 3) SignalR events – синхронне оновлення UI
//--------------------------------------------------
function reloadIfListAffected() {
    // Проста стратегія: якщо список чатів/grouplist є на сторінці — перезавантажити.
    // У production можна робити точкові DOM‑оновлення.
    location.reload();
}

// нова група
groupHub.on('GroupCreated', () => reloadIfListAffected());
// видалена група
groupHub.on('GroupDeleted', id => {
    if (window.chatConfig.currentGroupId == id) location.href = '/';
    else reloadIfListAffected();
});
// перейменовано
groupHub.on('GroupRenamed', (id, name) => {
    const li = document.querySelector(`li[data-group-id="${id}"] span`);
    if (li) li.textContent = name;
    if (window.chatConfig.currentGroupId == id) {
        const header = document.querySelector('.chat-container h5');
        if (header) header.textContent = name;
    }
});
// аватарка
groupHub.on('GroupAvatarChanged', (id, url) => {
    document.querySelectorAll(`li[data-group-id="${id}"] img`).forEach(img => img.src = url);
});
// додали учасника / забрали / вийшов
['GroupMemberAdded', 'GroupMemberRemoved', 'GroupMemberLeft'].forEach(evt => {
    groupHub.on(evt, () => {
        if (window.chatConfig.currentGroupId) reloadIfListAffected();
    });
});
// передано власника
groupHub.on('GroupOwnerTransferred', (id, newOwnerId) => {
    if (window.chatConfig.currentGroupId == id && window.chatConfig.currentUserId == newOwnerId) {
        alert('Ви стали власником групи');
    }
});

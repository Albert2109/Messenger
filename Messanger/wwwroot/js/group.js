
const GROUP_SETTINGS = {
    reconnectDelayMs: 5000,
    hubEndpoint: '/groupHub',
    api: {
        createGroup: '/Group/Create',
        addMember: groupId => `/Group/${groupId}/AddMember`,
        removeMember: groupId => `/Group/${groupId}/RemoveMember`,
        renameGroup: groupId => `/Group/${groupId}/Rename`,
        transferOwner: groupId => `/Group/${groupId}/TransferOwner`,
        leaveGroup: groupId => `/Group/${groupId}/Leave`,
        deleteGroup: groupId => `/Group/${groupId}/Delete`,
        changeAvatar: groupId => `/Group/${groupId}/Avatar`
    }
};

(() => {
    if (!window.chatConfig) {
        console.warn('chatConfig не знайдений – groupChat.js aborted');
        return;
    }

    document.addEventListener('DOMContentLoaded', () => {
        const { currentUserId, currentGroupId } = window.chatConfig;
        if (currentGroupId == null) return;

        const baseUrl = window.location.origin;
        const hubUrl = `${baseUrl}${GROUP_SETTINGS.hubEndpoint}?userId=${currentUserId}`;


        const groupHubConnection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
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
            console.log('✅ GroupHub reconnected, id:', connId)
        );
        groupHubConnection.onreconnecting(err =>
            console.warn('🔄 GroupHub reconnecting', err)
        );
        groupHubConnection.onclose(err =>
            console.error('❌ GroupHub closed', err)
        );
        (async function startGroupHub() {
            try {
                await groupHubConnection.start();
                console.log('🚀 Connected to GroupHub:', hubUrl);
            } catch (err) {
                console.warn(`⚠️ Retry in ${GROUP_SETTINGS.reconnectDelayMs} ms`, err);
                setTimeout(startGroupHub, GROUP_SETTINGS.reconnectDelayMs);
            }
        })();

        const btnNewGroup = document.getElementById('btnNewGroup');
        const newGroupModal = new bootstrap.Modal('#newGroupModal');
        const formNewGroup = document.getElementById('newGroupForm');
        const inputGroupName = document.getElementById('groupName');
        const inputGroupAvatar = document.getElementById('groupAva');
        const inputSearchUser = document.getElementById('userSearchInModal');
        const listSearchResults = document.getElementById('searchList');
        const listSelectedUsers = document.getElementById('chosenList');
        btnNewGroup?.addEventListener('click', () => newGroupModal.show());
        let searchDebounceId = 0;
        inputSearchUser?.addEventListener('input', () => {
            clearTimeout(searchDebounceId);
            const query = inputSearchUser.value.trim();
            if (query.length < 2) {
                listSearchResults.innerHTML = '';
                return;
            }
            searchDebounceId = setTimeout(async () => {
                const response = await fetch(`/Account/Search?q=${encodeURIComponent(query)}`);
                const users = await response.json();
                const selectedIds = Array.from(
                    listSelectedUsers.querySelectorAll('[data-id]')
                ).map(li => li.dataset.id);
                listSearchResults.innerHTML = users
                    .filter(u => !selectedIds.includes(u.id.toString()))
                    .map(u => `
            <li class="list-group-item d-flex align-items-center py-1">
              <img src="${u.ava}" class="avatar me-2" width="32" height="32">
              <span class="flex-grow-1">${u.login}</span>
              <button class="btn btn-sm btn-outline-primary"
                      data-add="${u.id}"
                      data-login="${u.login}"
                      data-ava="${u.ava}">+</button>
            </li>
          `).join('');
            }, 300);
        });
        listSearchResults?.addEventListener('click', e => {
            const addBtn = e.target.closest('button[data-add]');
            if (!addBtn) return;
            const userId = addBtn.dataset.add;
            const login = addBtn.dataset.login;
            const ava = addBtn.dataset.ava;
            if (!listSelectedUsers.querySelector(`[data-id="${userId}"]`)) {
                listSelectedUsers.insertAdjacentHTML('beforeend', `
          <li class="list-group-item d-flex align-items-center py-1" data-id="${userId}">
            <img src="${ava}" class="avatar me-2" width="32" height="32">
            <span class="flex-grow-1">${login}</span>
            <button class="btn btn-sm btn-outline-danger" data-remove>&times;</button>
          </li>
        `);
            }
            listSearchResults.innerHTML = '';
            inputSearchUser.value = '';
        });

        listSelectedUsers?.addEventListener('click', e => {
            const removeBtn = e.target.closest('button[data-remove]');
            if (removeBtn) removeBtn.closest('li')?.remove();
        });

        formNewGroup?.addEventListener('submit', async e => {
            e.preventDefault();
            const formData = new FormData();
            formData.append('name', inputGroupName.value.trim());
            if (inputGroupAvatar.files[0]) {
                formData.append('avatar', inputGroupAvatar.files[0]);
            }
            listSelectedUsers.querySelectorAll('li[data-id]').forEach(li => {
                formData.append('memberIds', li.dataset.id);
            });
            const res = await fetch(GROUP_SETTINGS.api.createGroup, {
                method: 'POST',
                body: formData
            });
            if (res.ok) {
                newGroupModal.hide();
                location.reload();
            } else {
                alert(await res.text() || 'Не вдалося створити групу');
            }
        });

        async function postForm(url, data) {
            const fd = new FormData();
            if (data) {
                Object.entries(data).forEach(([k, v]) => fd.append(k, v));
            }
            const resp = await fetch(url, { method: 'POST', body: fd });
            if (!resp.ok) throw new Error(await resp.text());
        }
        window.GroupApi = {
            addMember: (groupId, userId) => postForm(GROUP_SETTINGS.api.addMember(groupId), { userId }),
            removeMember: (groupId, userId) => postForm(GROUP_SETTINGS.api.removeMember(groupId), { userId }),
            renameGroup: (groupId, newName) => postForm(GROUP_SETTINGS.api.renameGroup(groupId), { name: newName }),
            transferOwner: (groupId, newOwnerId) => postForm(GROUP_SETTINGS.api.transferOwner(groupId), { newOwnerId }),
            leaveGroup: groupId => postForm(GROUP_SETTINGS.api.leaveGroup(groupId)),
            deleteGroup: groupId => postForm(GROUP_SETTINGS.api.deleteGroup(groupId)),
            changeAvatar: (groupId, file) => {
                const fd = new FormData();
                fd.append('file', file);
                return fetch(GROUP_SETTINGS.api.changeAvatar(groupId), { method: 'POST', body: fd });
            }
        };

        const reloadPage = () => location.reload();

        groupHubConnection.on('GroupCreated', reloadPage);
        groupHubConnection.on('GroupDeleted', groupId => {
            if (currentGroupId === groupId) {
                location.href = '/MessangerHome';
            } else reloadPage();
        });
        groupHubConnection.on('GroupRenamed', (groupId, newName) => {
            document
                .querySelectorAll(`li[data-group-id="${groupId}"] span`)
                .forEach(span => span.textContent = newName);
            if (currentGroupId === groupId) {
                const header = document.querySelector('.chat-container h5');
                if (header) header.textContent = newName;
            }
        });
        groupHubConnection.on('GroupAvatarChanged', (groupId, avatarUrl) => {
            document
                .querySelectorAll(`li[data-group-id="${groupId}"] img`)
                .forEach(img => img.src = avatarUrl);
        });

        ['GroupMemberAdded', 'GroupMemberRemoved', 'GroupMemberLeft']
            .forEach(eventName => groupHubConnection.on(eventName, reloadPage));

        groupHubConnection.on('GroupOwnerTransferred', (groupId, newOwnerId) => {
            if (currentGroupId === groupId && currentUserId === newOwnerId) {
                alert('Ви тепер власник групи');
            }
        });
    });
})();

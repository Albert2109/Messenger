

const GROUP_SETTINGS = {
    reconnectDelayMs: 5000,
    hubEndpoint: "/groupHub",
    api: {
        createGroup: "/Group/Create",
        addMember: id => `/Group/${id}/AddMember`,
        removeMember: id => `/Group/${id}/RemoveMember`,
        renameGroup: id => `/Group/${id}/Rename`,
        transferOwner: id => `/Group/${id}/TransferOwner`,
        leaveGroup: id => `/Group/${id}/Leave`,
        deleteGroup: id => `/Group/${id}/Delete`,
        changeAvatar: id => `/Group/${id}/Avatar`
    }
};

(() => {
    if (!window.chatConfig) {
        console.warn("chatConfig не знайдений – groupChat.js aborted");
        return;
    }

    document.addEventListener("DOMContentLoaded", () => {
        const { currentUserId, currentGroupId } = window.chatConfig;
        const $ = id => document.getElementById(id);

        const btnNewGroup = $("btnNewGroup");
        const newGroupModal = new bootstrap.Modal($("newGroupModal"));
        const formNewGroup = $("newGroupForm");
        const inputGroupName = $("groupName");
        const inputGroupAvatar = $("groupAva");
        const inputSearchUser = $("userSearchInModal");
        const listSearch = $("searchList");
        const listChosen = $("chosenList");

        btnNewGroup?.addEventListener("click", () => newGroupModal.show());
        let debounceId = 0;
        inputSearchUser?.addEventListener("input", () => {
            clearTimeout(debounceId);
            const q = inputSearchUser.value.trim();
            if (q.length < 2) { listSearch.innerHTML = ""; return; }
            debounceId = setTimeout(async () => {
                const res = await fetch(`/Account/Search?q=${encodeURIComponent(q)}`);
                const users = await res.json();
                const chosenIds = Array.from(listChosen.querySelectorAll("[data-id]"))
                    .map(li => li.dataset.id);
                listSearch.innerHTML = users
                    .filter(u => !chosenIds.includes(String(u.id)))
                    .map(u => `
                        <li class="list-group-item d-flex align-items-center py-1">
                            <img src="${u.ava}" class="avatar me-2" width="32" height="32">
                            <span class="flex-grow-1">${u.login}</span>
                            <button class="btn btn-sm btn-outline-primary" data-add="${u.id}" data-login="${u.login}" data-ava="${u.ava}">+</button>
                        </li>`)
                    .join("");
            }, 300);
        });

        listSearch?.addEventListener("click", e => {
            const btn = e.target.closest("button[data-add]");
            if (!btn) return;
            const { add: id, login, ava } = btn.dataset;
            if (!listChosen.querySelector(`[data-id='${id}']`)) {
                listChosen.insertAdjacentHTML("beforeend", `
                    <li class="list-group-item d-flex align-items-center py-1" data-id="${id}">
                        <img src="${ava}" class="avatar me-2" width="32" height="32">
                        <span class="flex-grow-1">${login}</span>
                        <button class="btn btn-sm btn-outline-danger" data-remove>&times;</button>
                    </li>`);
            }
            listSearch.innerHTML = "";
            inputSearchUser.value = "";
        });

        listChosen?.addEventListener("click", e => {
            const rm = e.target.closest("button[data-remove]");
            if (rm) rm.closest("li")?.remove();
        });

        formNewGroup?.addEventListener("submit", async e => {
            e.preventDefault();
            const fd = new FormData();
            fd.append("name", inputGroupName.value.trim());
            if (inputGroupAvatar.files[0]) fd.append("avatar", inputGroupAvatar.files[0]);
            listChosen.querySelectorAll("li[data-id]").forEach(li => fd.append("memberIds", li.dataset.id));
            const r = await fetch(GROUP_SETTINGS.api.createGroup, { method: "POST", body: fd });
            if (r.ok) { newGroupModal.hide(); location.reload(); }
            else alert(await r.text() || "Не вдалося створити групу");
        });
        async function postForm(url, data) {
            const fd = new FormData();
            Object.entries(data || {}).forEach(([k, v]) => fd.append(k, v));
            const resp = await fetch(url, { method: "POST", body: fd });
            if (!resp.ok) throw new Error(await resp.text());
        }

        window.GroupApi = {
            addMember: (g, u) => postForm(GROUP_SETTINGS.api.addMember(g), { userId: u }),
            removeMember: (g, u) => postForm(GROUP_SETTINGS.api.removeMember(g), { userId: u }),
            renameGroup: (g, n) => postForm(GROUP_SETTINGS.api.renameGroup(g), { name: n }),
            transferOwner: (g, u) => postForm(GROUP_SETTINGS.api.transferOwner(g), { newOwnerId: u }),
            leaveGroup: g => postForm(GROUP_SETTINGS.api.leaveGroup(g)),
            deleteGroup: g => postForm(GROUP_SETTINGS.api.deleteGroup(g)),
            changeAvatar: (g, f) => {
                const fd = new FormData();
                fd.append("file", f);
                return fetch(GROUP_SETTINGS.api.changeAvatar(g), { method: "POST", body: fd });
            }
        };
        if (currentGroupId == null) return; 

        const hubUrl = `${window.location.origin}${GROUP_SETTINGS.hubEndpoint}?userId=${currentUserId}`;
        const conn = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

        const reload = () => location.reload();


        conn.on("UserOnline", id => document.querySelectorAll(`.avatar[data-user-id='${id}']`).forEach(a => a.classList.add("online")));
        conn.on("UserOffline", id => document.querySelectorAll(`.avatar[data-user-id='${id}']`).forEach(a => a.classList.remove("online")));


        conn.on("GroupCreated", reload);
        conn.on("GroupDeleted", id => id === currentGroupId ? location.href = "/MessangerHome" : reload());
        conn.on("GroupRenamed", (id, name) => {
            document.querySelectorAll(`li[data-group-id='${id}'] span`).forEach(s => s.textContent = name);
            if (id === currentGroupId) document.querySelector(".chat-container h5")?.replaceChildren(name);
        });
        conn.on("GroupAvatarChanged", (id, url) => document.querySelectorAll(`li[data-group-id='${id}'] img`).forEach(img => img.src = url));
        ["GroupMemberAdded", "GroupMemberRemoved", "GroupMemberLeft"].forEach(evt => conn.on(evt, reload));
        conn.on("GroupOwnerTransferred", (id, newOwner) => {
            if (id === currentGroupId && newOwner === currentUserId) alert("Ви тепер власник групи");
        });


        conn.onclose(err => console.error("❌ GroupHub closed", err));
        conn.onreconnecting(err => console.warn("🔄 GroupHub reconnecting", err));
        conn.onreconnected(id => console.log("✅ GroupHub reconnected", id));

        (async function start() {
            try { await conn.start(); console.log("🚀 Connected to GroupHub", hubUrl); }
            catch (e) {
                console.warn(`⚠️ Retry in ${GROUP_SETTINGS.reconnectDelayMs} ms`, e);
                setTimeout(start, GROUP_SETTINGS.reconnectDelayMs);
            }
        })();
    });
})();
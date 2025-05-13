// -------------------------------------------------
//     wwwroot/js/group.js
//     Клієнтська логіка групових чатів
// -------------------------------------------------
/* global bootstrap, signalR, window, fetch */

// ──────────────────────────────────────────────────
// 0)  SignalR: підключення до GroupHub
// ──────────────────────────────────────────────────
const groupHub = new signalR.HubConnectionBuilder()
    .withUrl(`/groupHub?userId=${window.chatConfig.currentUserId}`)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

(async function startGroupHub() {
    try {
        await groupHub.start();
        console.log("✅ Connected to GroupHub");
    } catch (err) {
        console.warn("⚠️  GroupHub reconnect in 5 s", err);
        setTimeout(startGroupHub, 5_000);
    }
})();

// ──────────────────────────────────────────────────
// 1)  Модалка «Створити групу»
// ──────────────────────────────────────────────────
const btnNewGroup = document.getElementById("btnNewGroup");
const modalNewGroup = new bootstrap.Modal("#newGroupModal");

btnNewGroup?.addEventListener("click", () => modalNewGroup.show());

// елементи всередині модалки
const frmNew = document.getElementById("newGroupForm");
const inpName = document.getElementById("groupName");
const inpAva = document.getElementById("groupAva");
const inpQuery = document.getElementById("userSearchInModal");
const ulFound = document.getElementById("searchList");
const ulChosen = document.getElementById("chosenList");

// ---- live‑пошук користувачів ---------------------------------
let debounceId = 0;
inpQuery?.addEventListener("input", () => {
    clearTimeout(debounceId);
    const q = inpQuery.value.trim();

    if (q.length < 2) { ulFound.innerHTML = ""; return; }

    debounceId = setTimeout(async () => {
        const res = await fetch(`/Account/Search?q=${encodeURIComponent(q)}`);
        const users = await res.json(); // [{id, login, ava}, ...]
        // приховуємо вже обраних
        const chosenIds = Array.from(ulChosen.querySelectorAll("[data-id]"))
            .map(li => li.dataset.id);

        ulFound.innerHTML = users
            .filter(u => !chosenIds.includes(u.id.toString()))
            .map(u => `
        <li class="list-group-item d-flex align-items-center py-1">
          <img src="${u.ava}" class="avatar me-2" style="width:32px;height:32px">
          <span class="flex-grow-1">${u.login}</span>
          <button class="btn btn-sm btn-outline-primary"
                  data-add="${u.id}"
                  data-login="${u.login}"
                  data-ava="${u.ava}">+</button>
        </li>`).join("");
    }, 300);
});

// ---- додавання до списку «Учасники» ---------------------------
ulFound?.addEventListener("click", e => {
    const btn = e.target.closest("button[data-add]");
    if (!btn) return;

    const { add: id, login, ava } = btn.dataset;
    if (!id) return;

    if (!ulChosen.querySelector(`[data-id="${id}"]`)) {
        ulChosen.insertAdjacentHTML("beforeend", `
      <li class="list-group-item d-flex align-items-center py-1" data-id="${id}">
        <img src="${ava}" class="avatar me-2" style="width:32px;height:32px">
        <span class="flex-grow-1">${login}</span>
        <button class="btn btn-sm btn-outline-danger" data-remove>&times;</button>
      </li>`);
    }

    ulFound.innerHTML = "";
    inpQuery.value = "";
});

// ---- видалення зі списку -------------------------------------
ulChosen?.addEventListener("click", e => {
    const btn = e.target.closest("button[data-remove]");
    if (btn) btn.closest("li")?.remove();
});

// ---- сабміт форми -------------------------------------------
frmNew?.addEventListener("submit", async e => {
    e.preventDefault();

    const fm = new FormData();
    fm.append("name", inpName.value.trim());
    if (inpAva.files && inpAva.files.length) fm.append("avatar", inpAva.files[0]);

    ulChosen.querySelectorAll("li[data-id]")
        .forEach(li => fm.append("memberIds", li.dataset.id));

    const res = await fetch("/Group/Create", { method: "POST", body: fm });
    if (res.ok) {
        modalNewGroup.hide();
        location.reload();
    } else {
        alert(await res.text() || "Помилка створення групи");
    }
});

// ──────────────────────────────────────────────────
// 2)  API‑обгортка дій з групою
// ──────────────────────────────────────────────────
async function post(url, data) {
    const fm = new FormData();
    if (data) Object.entries(data).forEach(([k, v]) => fm.append(k, v));
    const r = await fetch(url, { method: "POST", body: fm });
    if (!r.ok) throw new Error(await r.text());
}

window.GroupApi = {
    addMember: (g, u) => post(`/Group/${g}/AddMember`, { userId: u }),
    removeMember: (g, u) => post(`/Group/${g}/RemoveMember`, { userId: u }),
    rename: (g, n) => post(`/Group/${g}/Rename`, { name: n }),
    transferOwner: (g, o) => post(`/Group/${g}/TransferOwner`, { newOwnerId: o }),
    leave: g => post(`/Group/${g}/Leave`),
    delete: g => post(`/Group/${g}/Delete`),
    changeAvatar: async (g, file) => {
        const fm = new FormData(); fm.append("file", file);
        await fetch(`/Group/${g}/Avatar`, { method: "POST", body: fm });
    }
};

// ──────────────────────────────────────────────────
// 3)  SignalR події (поки що — просте перезавантаження списку)
// ──────────────────────────────────────────────────
const refresh = () => location.reload();

groupHub.on("GroupCreated", refresh);
groupHub.on("GroupDeleted", id => {
    if (window.chatConfig.currentGroupId === id) {
        location.href = "/";
    } else refresh();
});
groupHub.on("GroupRenamed", (id, name) => {
    const span = document.querySelector(`li[data-group-id="${id}"] span`);
    if (span) span.textContent = name;

    if (window.chatConfig.currentGroupId === id) {
        const hdr = document.querySelector(".chat-container h5");
        if (hdr) hdr.textContent = name;
    }
});
groupHub.on("GroupAvatarChanged", (id, url) => {
    document.querySelectorAll(`li[data-group-id="${id}"] img`)
        .forEach(img => { img.src = url; });
});
["GroupMemberAdded", "GroupMemberRemoved", "GroupMemberLeft"]
    .forEach(evt => groupHub.on(evt, refresh));

groupHub.on("GroupOwnerTransferred", (id, newOwner) => {
    if (window.chatConfig.currentGroupId === id &&
        window.chatConfig.currentUserId === newOwner) {
        alert("Тепер ви власник групи");
    }
});

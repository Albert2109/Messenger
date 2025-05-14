(() => {
    const origin = window.location.origin;

    document.addEventListener('DOMContentLoaded', () => {
        const input = document.getElementById('userSearch');
        const list = document.getElementById('searchResults');
        if (!input || !list) return;

        let debounceTimer;
        input.addEventListener('input', e => {
            clearTimeout(debounceTimer);
            const q = e.target.value.trim();
            list.innerHTML = '';
            if (q.length < 2) return;

            debounceTimer = setTimeout(() => doSearch(q), 250); 
        });

        function doSearch(query) {
            fetch(`${origin}/Account/Search?q=${encodeURIComponent(query)}`)
                .then(r => r.json())
                .then(users => {
                    list.innerHTML = '';
                    users.forEach(u => {
                        const li = document.createElement('li');
                        li.className = 'list-group-item list-group-item-action';
                        li.textContent = `${u.login} (${u.email})`;
                        li.onclick = () => (window.location.search = `?chatId=${u.id}`);
                        list.appendChild(li);
                    });
                })
                .catch(err => console.error('User search error:', err));
        }
    });
})();

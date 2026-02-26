(function () {
    const input = document.getElementById("searchBox");
    const box = document.getElementById("searchSuggest");
    const form = input?.closest("form");

    if (!input || !box || !form) return;

    const MIN_LEN = 2;
    const DEBOUNCE_MS = 300;
    let timer = null;
    let lastQuery = "";
    let controller = null;
    let activeIndex = -1;

    function getItems() {
        return Array.from(box.querySelectorAll('[data-suggest-item="1"]'));
    }

    function clearActive() {
        const items = getItems();
        items.forEach(el => el.classList.remove("active"));
        activeIndex = -1;
    }

    function setActive(index) {
        const items = getItems();
        items.forEach(el => el.classList.remove("active"));

        if (items.length === 0) {
            activeIndex = -1;
            return;
        }

        activeIndex = Math.max(0, Math.min(index, items.length - 1));
        items[activeIndex].classList.add("active");
        items[activeIndex].scrollIntoView({ block: "nearest" });
    }

    function hide() {
        box.style.display = "none";
        box.innerHTML = "";
        clearActive();
    }

    async function loadSuggest(q) {
        if (controller) controller.abort();
        controller = new AbortController();

        const url = `/Search/Suggest?query=${encodeURIComponent(q)}`;

        const resp = await fetch(url, {
            method: "GET",
            headers: { "X-Requested-With": "XMLHttpRequest" },
            signal: controller.signal
        });

        if (!resp.ok) {
            hide();
            return;
        }

        const html = await resp.text();
        box.innerHTML = html.trim();

        if (box.innerHTML.length === 0) {
            hide();
        } else {
            box.style.display = "block";
            clearActive();
        }
    }

    input.addEventListener("input", function () {
        const q = (input.value || "").trim();

        if (q.length < MIN_LEN) {
            lastQuery = "";
            hide();
            return;
        }

        if (q === lastQuery) return;
        lastQuery = q;

        clearTimeout(timer);
        timer = setTimeout(() => {
            loadSuggest(q).catch(() => { });
        }, DEBOUNCE_MS);
    });

    input.addEventListener("keydown", function (e) {
        if (box.style.display === "none") return;

        const items = getItems();

        if (e.key === "ArrowDown") {
            if (items.length === 0) return;
            e.preventDefault();
            setActive(activeIndex < 0 ? 0 : activeIndex + 1);
            return;
        }

        if (e.key === "ArrowUp") {
            if (items.length === 0) return;
            e.preventDefault();
            setActive(activeIndex < 0 ? items.length - 1 : activeIndex - 1);
            return;
        }

        if (e.key === "Enter") {
            if (activeIndex >= 0 && activeIndex < items.length) {
                e.preventDefault();
                items[activeIndex].click();
            }
            return;
        }

        if (e.key === "Escape") {
            e.preventDefault();
            hide();
            return;
        }
    });

    box.addEventListener("mousemove", function (e) {
        const target = e.target.closest('[data-suggest-item="1"]');
        if (!target) return;

        const items = getItems();
        const idx = items.indexOf(target);
        if (idx >= 0 && idx !== activeIndex) setActive(idx);
    });

    document.addEventListener("click", function (e) {
        if (e.target === input || box.contains(e.target)) return;
        hide();
    });
})();
// Pinned troop shortcut management using localStorage
window.skojjtShortcuts = {
    _storageKey: 'pinnedTroops',

    _load: function () {
        try {
            return JSON.parse(localStorage.getItem(this._storageKey)) || [];
        } catch { return []; }
    },

    _save: function (shortcuts) {
        localStorage.setItem(this._storageKey, JSON.stringify(shortcuts));
    },

    getAll: function () {
        return this._load();
    },

    add: function (troopScoutnetId, name, url, scoutGroupName) {
        var shortcuts = this._load();
        // Remove existing entry for same troop to avoid duplicates
        shortcuts = shortcuts.filter(function (s) { return s.troopScoutnetId !== troopScoutnetId; });
        shortcuts.push({
            troopScoutnetId: troopScoutnetId,
            name: name,
            url: url,
            scoutGroupName: scoutGroupName,
            pinnedAt: new Date().toISOString()
        });
        this._save(shortcuts);
    },

    remove: function (troopScoutnetId) {
        var shortcuts = this._load();
        shortcuts = shortcuts.filter(function (s) { return s.troopScoutnetId !== troopScoutnetId; });
        this._save(shortcuts);
    },

    isPinned: function (troopScoutnetId) {
        return this._load().some(function (s) { return s.troopScoutnetId === troopScoutnetId; });
    }
};

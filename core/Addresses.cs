using System.Collections.Generic;

namespace gdmake {
    public static class Addresses {
        public static Dictionary<string, int> Names = new Dictionary<string, int> {
            { "MenuLayer.init", 0x1907b0 },
            { "EditLevelLayer.init", 0x6f5d0 },
            { "EditorUI.init", 0x76310 },
            { "KeybindingsLayer.init", 0x152f40 },
            { "KeysLayer.init", 0x154560 },
            { "LevelBrowserLayer.init", 0x15a040 },
            { "LevelEditorLayer.init", 0x15ee00 },
            { "LevelInfoLayer.init", 0x175df0 },
            { "LevelLeaderboard.init", 0x17c4f0 },
            { "LikeItemLayer.init", 0x18b9f0 },
            { "NumberInputLayer.init", 0x1982a0 },
            { "MoreVideoOptionsLayer.init", 0x1e2590 },
            { "MoreOptionsLayer.init", 0x1df6b0 },
            { "MoreOptionsLayer.addToggle", 0x1DF6B0 },
            { "PlayerObject.init", 0x1e6da0 },
            { "PlayerObject.pushButton", 0x1f4e40 },
            { "PlayerObject.resetObject", 0x1eecd0 },
            { "PlayLayer.init", 0x1fb780 },
            { "ProfilePage.init", 0x20ef00 },
            { "RateDemonLayer.init", 0x214180 },
            { "SetGroupIDLayer.init", 0x22b670 },
            { "ShareCommentLayer.init", 0x24bb90 },
        };

        public static void LoadUserAddresses() {
            if (!GDMake.IsGlobalInitialized())
                return;

            foreach (var addr in GDMake.SettingsFile.Addresses)
                Names.Add(addr.Item1, addr.Item2);
        }
    }
}
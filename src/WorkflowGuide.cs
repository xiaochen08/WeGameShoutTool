namespace WeGameShoutTool
{
    public static class WorkflowGuide
    {
        public const string ProductSignature = "B站:bia哩bia哩";
        public const string ProductHomeUrl = "https://space.bilibili.com/53391408?spm_id_from=333.788.0.0";
        public const string SecuritySoftwareNotice = "安全软件可能拦截本工具关闭 WeGame 子进程或写入 WeGame 缓存。若安全软件弹出拦截，请仅允许本工具本次操作，或将当前工具 EXE 加入信任列表。不要退出或长期关闭全部防护；本工具也不会自动修改安全软件设置。";
        public const string CustomOverrideFallbackNotice = "已打开 WeGame 自定义修改功能。\r\n\r\n如果软件未生效，请在 WeGame 页面手动修改并保存即可。";

        public static string GetInstructionForTesting(
            bool weGameRunning,
            bool hasSeenWeGameRunning,
            bool contentValid,
            bool contentConfirmed,
            bool patchBusy,
            bool patchApplied)
        {
            if (patchBusy)
            {
                return "正在应用，请稍候。";
            }
            if (patchApplied)
            {
                return "强力覆盖补丁已安装。\r\n\r\n" + CustomOverrideFallbackNotice + "\r\n\r\n请在 WeGame 打开英雄联盟辅助设置，等工具显示“强力覆盖：已生效”后再启动游戏。";
            }
            if (!hasSeenWeGameRunning && !weGameRunning)
            {
                return "第1步：目前未检测到 WeGame 正在运行，请先打开 WeGame。";
            }
            if (!contentValid)
            {
                return "第2步：准备好10条喊话，然后点击①保存。";
            }
            if (!contentConfirmed)
            {
                return "第2步：请先点击①保存。";
            }
            return "第3步：点击②，工具会安装强力覆盖补丁并重新打开 WeGame。";
        }

        public static string GetApplyBlockMessageForTesting(
            bool weGameRunning,
            bool hasSeenWeGameRunning,
            bool contentValid,
            bool contentConfirmed,
            bool patchBusy)
        {
            if (patchBusy)
            {
                return "正在应用，请稍候，不要重复点击。";
            }
            if (!hasSeenWeGameRunning)
            {
                return "目前未检测到 WeGame 正在运行，请先打开 WeGame。\r\n\r\n检测到以后，请退出 WeGame，再点击②应用。";
            }
            if (!contentValid)
            {
                return "喊话内容还没准备好。\r\n\r\n请准备好10条内容，再点击①保存。";
            }
            if (!contentConfirmed)
            {
                return "还没有保存内容。\r\n\r\n请先点击①保存，再点击②应用。";
            }
            return null;
        }
    }
}

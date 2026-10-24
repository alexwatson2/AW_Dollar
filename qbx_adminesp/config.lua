Config = {}

-- ============================================================================
-- دستورات و کلیدهای میانبر | Commands & Keybinds
-- ============================================================================
Config.Commands = {
    'esp',
    'adminesp',
}

-- دکمه باز کردن پنل تنظیمات ESP (قابل تغییر توسط هر ادمین در تنظیمات Key Bindings بازی)
-- Set to false or '' to disable default keybind
Config.OpenMenuKey = 'F10'

-- دکمه خاموش/روشن سریع ESP بدون باز کردن منو (اختیاری)
Config.QuickToggleKey = ''

-- ============================================================================
-- تنظیمات دسترسی ادمین در فریم‌ورک Qbox | Qbox Admin Permissions
-- ============================================================================
-- رنک‌ها و ACE پرمیشن‌های مجاز در Qbox (بررسی به صورت ۱۰۰٪ سمت سرور انجام می‌شود)
-- در Qbox به صورت پیش‌فرض در permissions.cfg گروه‌های group.admin و group.god تعریف شده‌اند
Config.AdminPermissions = {
    'admin',
    'god',
    'group.admin',
    'group.god',
    'qbx.admin',
    'qbx.god',
    -- 'mod',       -- در صورت تمایل به دسترسی هلپر/مدریتور از حالت کامنت خارج کنید
    -- 'group.mod',
}

-- اگر true باشد، ادمین باید در حالت Admin Duty / Opt-in فریم‌ورک Qbox باشد (/optin)
Config.RequireOptIn = false

-- لیست اختیاری لایسنس یا دیسکورد ادمین‌های خاص (علاوه بر سیستم رنک Qbox)
Config.WhitelistedIdentifiers = {
    -- 'license2:xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx',
    -- 'discord:123456789012345678',
}

-- ثبت لاگ روشن/خاموش شدن ESP توسط ادمین در کنسول سرور و سیستم لاگ
Config.EnableAuditLog = true

-- بازه همگام‌سازی اطلاعات کاراکترهای Qbox (نام کاراکتر، شغل، مختصات دور در OneSync) به میلی‌ثانیه
Config.ServerSyncInterval = 3000

-- ============================================================================
-- مقادیر پیش‌فرض تنظیمات پنل | Default ESP & Aim Assist Settings
-- ============================================================================
Config.DefaultSettings = {
    -- کلید اصلی فعال‌سازی ESP
    enabled = false,

    -- تنظیمات اصلی خواسته شده | Core Requested Features
    playerEsp = true,
    playerColor = '#FF0000', -- Default: Red (قرمز)

    npcEsp = true,
    npcColor = '#00FF00',    -- Default: Green (سبز)

    aimAssist = false,

    -- جزئیات ظاهری ESP (همه قابلیت‌ها) | Full ESP Visual Features
    boxEsp = true,
    boxStyle = 'corner',     -- 'corner' | '2d' | '3d'
    boxFill = true,          -- هاله رنگی شفاف داخل کادر (Chams Fill)
    skeleton = true,         -- نمایش اسکلت و استخوان‌بندی کاراکتر
    headDot = true,          -- نقطه مشخص‌کننده سر (Head Indicator)
    tracers = true,          -- خطوط راهنما (Snaplines / Tracers)
    tracerOrigin = 'bottom', -- 'bottom' | 'center' | 'ped'
    names = true,            -- نمایش نام پلیر / نام کاراکتر Qbox / آیدی سرور
    healthBar = true,        -- نوار خون (HP) و زره (Armor) کنار کادر
    distance = true,         -- نمایش فاصله به متر
    weaponInfo = true,       -- نمایش نام سلاح در دست پلیر/NPC
    showDead = false,        -- نمایش پلیرها و NPCهای کشته شده
    adminBlips = false,      -- نمایش مارکر پلیرها و NPCها روی رادار/مینی‌مپ
    showStatusHud = true,    -- نشانگر کوچک وضعیت فعال بودن ESP گوشه تصویر
    maxDistance = 300,       -- حداکثر فاصله رندر به متر (25 تا 1000)

    -- تنظیمات پیشرفته Aim Assist | Aim Assist Settings
    aimTargetPlayers = true, -- قفل روی پلیرها
    aimTargetNpcs = true,    -- قفل روی NPCها
    showFov = true,          -- نمایش دایره محدوده FOV وسط صفحه
    fovRadius = 120,         -- شعاع دایره FOV (30 تا 350)
    fovColor = '#FFFFFF',    -- رنگ دایره FOV
    aimSmooth = 4.5,         -- نرمی حرکت دوربین (1.0 = سریع، 15.0 = بسیار نرم)
    aimBone = 'head',        -- 'head' | 'neck' | 'chest' | 'pelvis'
    aimVisibleOnly = true,   -- عدم قفل از پشت دیوار (Line of Sight Check)
    aimMaxDistance = 150,    -- حداکثر فاصله عملکرد Aim Assist به متر
    aimTargetLine = true,    -- نمایش خط اتصال به هدف قفل شده در FOV
}

-- ============================================================================
-- جدول نام سلاح‌ها برای نمایش روی ESP | Weapon Hash Labels
-- ============================================================================
Config.WeaponNames = {
    [`WEAPON_UNARMED`] = 'Unarmed',
    [`WEAPON_KNIFE`] = 'Knife',
    [`WEAPON_NIGHTSTICK`] = 'Nightstick',
    [`WEAPON_HAMMER`] = 'Hammer',
    [`WEAPON_BAT`] = 'Baseball Bat',
    [`WEAPON_CROWBAR`] = 'Crowbar',
    [`WEAPON_DAGGER`] = 'Dagger',
    [`WEAPON_MACHETE`] = 'Machete',
    [`WEAPON_SWITCHBLADE`] = 'Switchblade',
    [`WEAPON_BATTLEAXE`] = 'Battle Axe',
    [`WEAPON_KATANA`] = 'Katana',

    -- Pistols
    [`WEAPON_PISTOL`] = 'Pistol',
    [`WEAPON_PISTOL_MK2`] = 'Pistol Mk II',
    [`WEAPON_COMBATPISTOL`] = 'Combat Pistol',
    [`WEAPON_APPISTOL`] = 'AP Pistol',
    [`WEAPON_STUNGUN`] = 'Taser',
    [`WEAPON_PISTOL50`] = 'Pistol .50',
    [`WEAPON_SNSPISTOL`] = 'SNS Pistol',
    [`WEAPON_SNSPISTOL_MK2`] = 'SNS Pistol Mk II',
    [`WEAPON_HEAVYPISTOL`] = 'Heavy Pistol',
    [`WEAPON_VINTAGEPISTOL`] = 'Vintage Pistol',
    [`WEAPON_REVOLVER`] = 'Heavy Revolver',
    [`WEAPON_REVOLVER_MK2`] = 'Revolver Mk II',
    [`WEAPON_DOUBLEACTION`] = 'Double Action',
    [`WEAPON_CERAMICPISTOL`] = 'Ceramic Pistol',
    [`WEAPON_NAVYREVOLVER`] = 'Navy Revolver',
    [`WEAPON_TECPISTOL`] = 'Tactical SMG',

    -- SMGs & Shotguns
    [`WEAPON_MICROSMG`] = 'Micro SMG',
    [`WEAPON_SMG`] = 'SMG',
    [`WEAPON_SMG_MK2`] = 'SMG Mk II',
    [`WEAPON_ASSAULTSMG`] = 'Assault SMG',
    [`WEAPON_COMBATPDW`] = 'Combat PDW',
    [`WEAPON_MACHINEPISTOL`] = 'Machine Pistol',
    [`WEAPON_MINISMG`] = 'Mini SMG',
    [`WEAPON_PUMPSHOTGUN`] = 'Pump Shotgun',
    [`WEAPON_PUMPSHOTGUN_MK2`] = 'Pump Shotgun Mk II',
    [`WEAPON_SAWNOFFSHOTGUN`] = 'Sawed-Off',
    [`WEAPON_ASSAULTSHOTGUN`] = 'Assault Shotgun',
    [`WEAPON_BULLPUPSHOTGUN`] = 'Bullpup Shotgun',
    [`WEAPON_HEAVYSHOTGUN`] = 'Heavy Shotgun',
    [`WEAPON_COMBATSHOTGUN`] = 'Combat Shotgun',

    -- Rifles & LMGs
    [`WEAPON_ASSAULTRIFLE`] = 'Assault Rifle',
    [`WEAPON_ASSAULTRIFLE_MK2`] = 'Assault Rifle Mk II',
    [`WEAPON_CARBINERIFLE`] = 'Carbine Rifle',
    [`WEAPON_CARBINERIFLE_MK2`] = 'Carbine Rifle Mk II',
    [`WEAPON_ADVANCEDRIFLE`] = 'Advanced Rifle',
    [`WEAPON_SPECIALCARBINE`] = 'Special Carbine',
    [`WEAPON_SPECIALCARBINE_MK2`] = 'Special Carbine Mk II',
    [`WEAPON_BULLPUPRIFLE`] = 'Bullpup Rifle',
    [`WEAPON_BULLPUPRIFLE_MK2`] = 'Bullpup Rifle Mk II',
    [`WEAPON_COMPACTRIFLE`] = 'Compact Rifle',
    [`WEAPON_MILITARYRIFLE`] = 'Military Rifle',
    [`WEAPON_HEAVYRIFLE`] = 'Heavy Rifle',
    [`WEAPON_TACTICALRIFLE`] = 'Service Carbine',
    [`WEAPON_MG`] = 'MG',
    [`WEAPON_COMBATMG`] = 'Combat MG',
    [`WEAPON_COMBATMG_MK2`] = 'Combat MG Mk II',
    [`WEAPON_GUSENBERG`] = 'Gusenberg',

    -- Snipers & Heavy
    [`WEAPON_SNIPERRIFLE`] = 'Sniper Rifle',
    [`WEAPON_HEAVYSNIPER`] = 'Heavy Sniper',
    [`WEAPON_HEAVYSNIPER_MK2`] = 'Heavy Sniper Mk II',
    [`WEAPON_MARKSMANRIFLE`] = 'Marksman Rifle',
    [`WEAPON_MARKSMANRIFLE_MK2`] = 'Marksman Rifle Mk II',
    [`WEAPON_RPG`] = 'RPG',
    [`WEAPON_MINIGUN`] = 'Minigun',
    [`WEAPON_HOMINGLAUNCHER`] = 'Homing Launcher',
}

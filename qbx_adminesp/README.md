# Qbox Admin ESP & Aim Assist (`qbx_adminesp`)

ریسورس کامل، امن و بهینه **Admin ESP & Aim Assist** مخصوص سرورهای **FiveM** با فریم‌ورک **Qbox (`qbx_core` + `ox_lib`)**.
تمامی بررسی‌های دسترسی ادمین به صورت **۱۰۰٪ سمت سرور (Server-Side)** انجام می‌شود؛ بنابراین پلیرهای عادی به هیچ عنوان امکان باز کردن منو، فعال‌سازی ESP یا دریافت اطلاعات سایر پلیرها را ندارند.

---

## ویژگی‌های اصلی

- **احراز هویت کامل سمت سرور با فریم‌ورک Qbox:**
  - تشخیص خودکار رنک‌های ادمین Qbox از طریق سیستم استاندارد ACE (`group.admin`, `group.god`, `admin`, `god`) و `exports.qbx_core:HasPermission`.
  - بررسی مداوم سطح دسترسی: اگر رنک ادمین در حین بازی تغییر کند یا گرفته شود، ESP و Aim Assist بلافاصله توسط سرور خاموش می‌شوند.
- **تنظیمات کامل و ذخیره خودکار (KVP):**
  - `Player ESP`: روشن/خاموش (`true / false`) + انتخاب رنگ دلخواه با Color Picker و کد Hex (**پیش‌فرض: قرمز `#FF0000`**)
  - `NPC ESP`: روشن/خاموش (`true / false`) + انتخاب رنگ دلخواه با Color Picker و کد Hex (**پیش‌فرض: سبز `#00FF00`**)
  - `Aim Assist`: روشن/خاموش (`true / false`) + تنظیم شعاع و رنگ دایره FOV، نرمی حرکت دوربین (`Smoothness`)، استخوان هدف (`Head / Neck / Chest / Pelvis`)، بررسی مانع/دیوار (`Visible Only`) و فیلتر اهداف (`Players / NPCs`).
- **شامل تمام حالت‌های نمایشی ESP:**
  - **Box ESP:** سه حالت `Corner Box`، `Full 2D Box` و `3D Bounding Box` به همراه هایلایت نیمه‌شفاف داخل باکس (`Chams Fill`)
  - **Skeleton ESP:** نمایش دقیق استخوان‌بندی و مفاصل کاراکتر
  - **Head Dot:** نقطه مشخص‌کننده سر کاراکتر
  - **Tracer Lines (Snaplines):** خطوط راهنما با قابلیت انتخاب مبدأ (`Bottom`، `Center` یا `Admin Ped`)
  - **اطلاعات کامل Qbox:** نمایش `[Server ID] Firstname Lastname (AccountName)`، نوار عمودی خون (`HP`) و جلیقه (`Armor`)، فاصله به متر (`Distance`) و نام اسلحه در دست (`Weapon Info`)
  - **Admin Radar Blips:** نمایش مارکر پلیرها و NPCها به همراه جهت نگاه روی مینی‌مپ
  - **پشتیبانی از OneSync Infinity:** نمایش پلیرهای دورتر از محدوده استریم کلاینت تا سقف فاصله انتخابی
- **بهینه‌سازی کامل (`0.00ms` در حالت خاموش):**
  - زمانی که ESP خاموش است، هیچ حلقه رندری در کلاینت اجرا نمی‌شود (`0.00ms` در `resmon`).

---

## آموزش نصب در سرور Qbox

۱. پوشه **`qbx_adminesp`** را در مسیر ریسورس‌های سرور خود کپی کنید (مثلاً `resources/[qbx]/qbx_adminesp`).

۲. در فایل `server.cfg` (بعد از `ox_lib` و `qbx_core`) خط زیر را اضافه کنید:

```cfg
ensure ox_lib
ensure qbx_core
ensure qbx_adminesp
```

۳. مطمئن شوید در `permissions.cfg` یا `server.cfg` سرور Qbox شما دسترسی گروه ادمین تعریف شده است (در سرورهای استاندارد Qbox از قبل وجود دارد):

```cfg
add_ace group.admin admin allow
add_ace group.god god allow
add_principal identifier.fivem:XXXXXX group.admin
```

*(همچنین در صورت نیاز می‌توانید لایسنس یا دیسکورد ادمین خاصی را هم در `config.lua` بخش `Config.WhitelistedIdentifiers` اضافه کنید.)*

---

## دستورات و کلیدهای میانبر

| دستور / کلید | کاربرد |
| --- | --- |
| `/esp` یا `/adminesp` | باز کردن پنل گرافیکی تنظیمات Admin ESP (فقط ادمین) |
| `/esp toggle` | روشن یا خاموش کردن سریع ESP بدون باز کردن منو |
| کلید `F10` | کلید میانبر باز/بسته کردن پنل (قابل تغییر توسط هر ادمین در `Settings > Key Bindings > FiveM`) |

---

## ساختار فایل‌ها

```text
qbx_adminesp/
├── fxmanifest.lua       # مانیفست ریسورس FiveM (Lua 5.4 + ox_lib + qbx_core)
├── config.lua           # تنظیمات پرمیشن‌ها، کلیدها، مقادیر پیش‌فرض (قرمز/سبز) و جدول سلاح‌ها
├── server/
│   └── main.lua         # بررسی امنیتی رنک ادمین Qbox، دستورات، لاگ کنسول و سینک OneSync
├── client/
│   ├── main.lua         # مدیریت وضعیت، ذخیره KVP، ارتباط با NUI و کلیدهای میانبر
│   ├── esp.lua          # اسکنر بهینه و موتور رندر 2D/3D Box, Skeleton, Tracers, HP/Armor, Blips
│   └── aimassist.lua    # سیستم Aim Assist نرم و دقیق با محاسبه FOV و دید مستقیم (LOS)
└── html/
    ├── index.html       # رابط کاربری پنل مدیریت و لایه دایره FOV
    ├── style.css        # استایل مدرن و شفاف NUI
    └── script.js        # منطق تعاملی پنل و ارتباط با کلاینت Lua
```

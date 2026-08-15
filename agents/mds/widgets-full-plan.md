# خطة Widgets الكاملة متعددة المنصات

## قاعدة الحالة

الحالتان المسموحتان فقط هما **لم تتم الإضافة** و**تمت الإضافة**. وجود كود أو نجاح اختبارات جزئية لا يكفي لتغيير الحالة. لا تتغير أي حالة إلى **تمت الإضافة** إلا بعد نجاح البناء والتثبيت والقبول الحقيقي على المنصة، ولا تظهر ميزة غير مكتملة في Production.

| المكوّن | الحالة |
| --- | --- |
| Core والعقود المشتركة | لم تتم الإضافة |
| محرر React والمعاينة | لم تتم الإضافة |
| Android Home/Lock Screen | لم تتم الإضافة |
| iOS/iPadOS WidgetKit | لم تتم الإضافة |
| Windows 11 System Widget | لم تتم الإضافة |
| Windows 10/11 Companion | لم تتم الإضافة |
| القالب العام | لم تتم الإضافة |

## سجل التنفيذ المثبت حتى الآن

- Core: العقود، Profiles الذرية، revision، CRUD/RPC، Projection، RenderTree، resolver، الخصوصية، منع overflow الصامت، Android capabilities، Windows Adaptive Cards وprojection store موجودة. آخر تشغيل محلي: **269/269** اختبار .NET ناجح. تبقى الحالة **لم تتم الإضافة** إلى أن تكتمل بوابات المنصات المستهلكة لها.
- React: المحرر والمعاينة من RenderTree التابعة لـCore موجودان خلف `VITE_WIDGET_DEVELOPMENT=true` فقط. السيناريو `12-widget-editor` نجح بـ19 assertion وصفر warnings، وكل RPC المقاسة أقل من 300ms. لا يظهر المحرر في Production، لذلك تبقى الحالة **لم تتم الإضافة** حتى قبول المنصات ولقطات المصفوفة كاملة.
- Android: RemoteViews وChronometer وresolver للحجم/Keyguard والترحيل والسجلات موجودة. Receivers معطلة في البناء العادي، ولا تُفعّل إلا مع `WidgetDevelopment=true`. Debug العادي نجح بلا تحذيرات؛ لا يوجد بعد تثبيت وقبول Home/Keyguard على الجهاز، لذلك الحالة **لم تتم الإضافة**.
- Windows 11: Provider معزول يبنى Debug/Release بلا تحذيرات، ويسجل instances ويرتبط بملف Profiles، والتطبيق ينشر RenderTrees للأحجام small/medium/large. لم تُنشأ بعد الحزمة MSIX الموقعة الواحدة ولم ينجح install/upgrade أو Widgets Board UI Automation؛ الحالة **لم تتم الإضافة**.
- iOS وWindows Companion: المصدر المعزول موجود فقط، ولا يدخل Production. لا يوجد قبول Mac/iPhone أو Companion حقيقي؛ الحالتان **لم تتم الإضافة**.
- القالب: `widgetSupport=none|cross-platform` وافتراضه `none`. تم إنشاء مشروع عام باسم `FutureApp` وتشغيل **3/3** اختبارات Release حقيقية بلا أسماء PrayAdFree، كما بُني مشروع `PlainApp` الافتراضي دون تسرب ملفات Widgets. تبقى الحالة **لم تتم الإضافة** حتى إضافة واختبار نقاط الترحيل والـhost renderers كاملة.

## المصدر المشترك

- `PrayAdFree.Core` هو المصدر الوحيد للحسابات والبيانات وقواعد اختيار المحتوى.
- يضاف `WidgetProfile` مستقل مع الاسم، القالب، revision، الكثافة الدلالية، ترتيب عناصر Projection، حجم الخط الدلالي، الألوان، الشفافية، الثيم والخصوصية.
- يضاف `WidgetHostCapabilities` لوصف المنصة والسطح والعائلة والمساحة وعدد العناصر والأفعال المدعومة.
- يضاف `WidgetProjection` للبيانات المحسوبة و`WidgetRenderTree` للنصوص والصفوف والتقدم والأفعال وAccessibility.
- `WidgetLayoutResolver` وحده يقرر ما يظهر حسب Profile وإمكانات المضيف. Renderers لا تحسب الصلاة ولا تختار المحتوى.
- تحفظ Profiles وارتباطات instances ذرياً، مع ترحيل Widgets الحالية وحفظ حالة التسبيح.
- تضاف RPCs: catalog، profiles، create، patch، duplicate، delete، preview، installed instances وassign profile. كل mutation يعيد الحالة المؤكدة كاملة، وكل RPC محلي دون 300ms وبلا read لاحق أو طلب مكرر.

## القوالب والمحرر

القوالب الأساسية: الصلاة القادمة، جدول اليوم، الصيام، التسبيح، التاريخ والصلاة، وزاوية القبلة الثابتة. يمكن إنشاء ونسخ وتسمية وتعديل وحذف Profile وربطها بكل instance.

محرر React يعرض Preview حي من `WidgetRenderTree` نفسها لمنصات Android وiPhone وWindows، ويمنع الحفظ عند overflow أو contrast غير صالح. المستخدم يختار Dimension دلالية وProjection ولا يدخل Pixels. لا تتحول Widget إلى صورة؛ النصوص والأفعال تبقى أصلية وقابلة للوصول.

## المنصات

- Android يبقي `RemoteViews` كـrenderer رفيع، يقرأ الحجم والـhost category الفعليين، ويدعم Home وKeyguard حيث يدعم الجهاز ذلك. إلى أن ينجح القبول الحقيقي، تبنى receivers معطلة افتراضياً ولا تُفعّل إلا ببناء `WidgetDevelopment=true`؛ لذلك لا تظهر ميزة ناقصة في Production.
- iOS يضيف WidgetKit Extension وApp Group وAppIntent لاختيار Profile، مع عائلات accessory وsystem وAlways-On وRTL وDynamic Type.
- Windows 11 ينتقل إلى حزمة MSIX واحدة تضم التطبيق وWidget Provider عالمي Offline مبني على Adaptive Cards، مع ترحيل بيانات النسخة unpackaged.
- Windows 10/11 Companion نافذة React ثانية داخل التطبيق نفسه، بلا إطار أو أزرار نظام، حجمها من الإعدادات وموقعها بالسحب. زر X يغلق النافذة فقط، والتشغيل مع Windows اختياري ومغلق افتراضياً.
- القالب العام يحصل على العقود والمعاينة ونقاط امتداد Renderers وخيار `widgetSupport=none|cross-platform` وافتراضه `none`.

## الأخطاء والتحديث

- لا fallback لموقع أو طريقة حساب أو أوقات مختلقة. نقص البيانات يظهر خطأ واضحاً وآخر وقت تحديث.
- تغير الموقع والحساب واللغة والثيم والمنطقة الزمنية وحدود الصلاة يولد Projection واحدة ثم يحدث instances.
- الأخطاء تسجل المنصة وinstance/profile والحجم/revision ومدة Projection والرندرة في ملف.
- شاشة القفل تخفي الموقع التفصيلي افتراضياً.

## الاختبارات والقبول

- Core: كل قالب وحجم ولغة و12/24 ساعة ومنتصف الليل وبعد العشاء والمنطقة الزمنية والمواقع القطبية ونقص البيانات، إضافة إلى property/migration/RPC/performance tests.
- React: كل control وقيم قبل/بعد وCRUD والربط، screenshots لكل منصة وحجم ولغة وثيم وخط، overflow وcontrast وRTL وkeyboard وAccessibility.
- Android: size/capability tests، محاكيات قديم/حديث، جهاز Samsung حقيقي، Home/Keyguard، resize/restart/locale/location/update، screenshots وAccessibility.
- iOS: XCTest وsnapshots لكل family، ثم Release على Mac وتثبيت تحديث واختبار قفل وشاشة رئيسية على iPhone حقيقي. حتى ذلك تبقى **لم تتم الإضافة**.
- Windows: Adaptive Card tests، MSIX install/upgrade/migration، Windows 11 UI Automation، وCompanion على Windows 10/11 وDPI وmulti-monitor.
- لكل منصة وقالب تقرير تحت `agents/mds/widget-acceptance/` بعناوين `##input`, `##Actions`, `##Tested`, `##Faild+why`, `##things to fix`, `##remarks`.
- أي فشل أو BLOCKED يبقي الحالة **لم تتم الإضافة**. لا Release قبل صفر أخطاء وoverflow وmixed language وفقد بيانات، وكل العمليات المحلية دون 300ms.

# EzAcrossControl

**اتصل. تحكّم. واصل.** · by ElementZero

[جميع اللغات والدليل التقني الكامل (بالبرتغالية)](../../README.md)

تحكّم بجهاز Android باستخدام فأرة Windows ولوحة مفاتيحه. انقل المؤشر إلى حافة الشاشة المحددة للتبديل بين الجهازين. يتواصل التطبيقان عبر الشبكة المحلية دون حساب أو توجيه عبر السحابة.

## تنزيل الإصدار Windows 1.1.4 / Android 1.1.7

- [مثبّت Windows](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-setup.exe)
- [ملف Android APK](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.7/EZAcrossControl-1.1.7-android.apk)
- [نسخة Windows المحمولة ZIP](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-portable.zip)
- [الإصدارات وفحوص السلامة](https://github.com/eddesignerez/EzAcrossControl/releases)

## المتطلبات والاتصال الأول

يلزم Windows 10/11 بنظام 64 بت، وAndroid 7 أو أحدث، واتصال الجهازين بالشبكة المحلية نفسها، حتى عند اختيار USB للتحكم الأصلي. اسمح بتصحيح أخطاء USB أو أقرن تصحيح الأخطاء اللاسلكي (Android 11 أو أحدث). يعتمد التحكم الأصلي على دعم الجهاز لتقنية UHID.

1. ثبّت EzAcrossControl وافتحه على Windows. يطلب المثبّت صلاحية المسؤول لإعداد الخادم المحلي وجدار حماية الشبكة الخاصة.
2. ثبّت APK. على Android افتح **الوضع المتقدم ← تصحيح الأخطاء ← فتح الإعدادات**، وفعّل خيارات المطوّر وطريقة تصحيح الأخطاء، ثم اسمح للكمبيوتر بالاتصال.
3. لتصحيح الأخطاء اللاسلكي، أقرن الجهاز باستخدام ADB المرفق وفق [الدليل المفصل](../../README.md#primeiro-uso). منفذ الاقتران يختلف عن منفذ الاتصال.
4. اختر في APK أحد الخيارات **Auto** أو **Wi-Fi** أو **USB**. أدخل عنوان IP الظاهر على Windows والمنفذ نفسه في التطبيقين (الافتراضي **8765**). اضغط **اتصال** وحدد حافة شاشة Android في Windows.

يتبع Android لغة النظام افتراضيًا. يمكنك تغييرها من **الوضع المتقدم ← اللغة**؛ ويوجد محدد اللغة في قسم التشخيص على Windows.

## معاينات بصرية وحدود التحقق

![مقترح Day ثابت](../../Brand/Assets/PNG/splash-day-1600x900.png)
![مقترح Night ثابت](../../Brand/Assets/PNG/splash-night-1600x900.png)

هذه **مقترحات هوية بصرية ثابتة وليست لقطات شاشة للتطبيق أثناء تشغيله**. ما زالت الخطوط وتنسيقات الألوان بانتظار الموافقة. اقتصر الاختبار الفعلي على HiPadPlus وLAVIE T11؛ ويلزم التحقق من الأجهزة الأخرى على حدة. مثبّت Windows غير موقّع بتوقيع Authenticode. راجع [استكشاف الأخطاء والبنية](../../README.md#solução-de-problemas) و[إشعارات الأطراف الثالثة](../../THIRD-PARTY-NOTICES.md).

using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Seed;

/// <summary>
/// Idempotent platform-agreement seed. Inserts the canonical 3 agreements
/// (Kullanım Koşulları, KVKK Aydınlatma, Ticari Elektronik İleti İzni) if no
/// active version exists yet for a code. Existing edits made via continuo-ops-ui
/// are never overwritten — only the first-boot defaults are planted.
/// <para>
/// Hukuki sorumluluk: bu metinler kafe/SaaS sektörü için sektörel taslaktır.
/// Platform sahibi (yayına çıkmadan) continuo-ops-ui üzerinden hukuk danışmanı
/// onayı ile gözden geçirmeli ve şirket bilgilerini doldurmalıdır. KVKK
/// Aydınlatma Yükümlülüğü m.10 + Ticari İletişim Yön. m.6 + İYS zorunluluğu
/// bu metinlerin canlı tutulmasını zorunlu kılar.
/// </para>
/// </summary>
public static class DefaultPlatformAgreements {
    public const string CodeTerms = "terms";
    public const string CodeKvkk = "kvkk";
    public const string CodeMarketing = "marketing";

    /// <summary>
    /// Tenant kayıt akışında (Abone = kafe/restoran işletmesi) imzalanan
    /// platform-düzeyi SaaS Hizmet Abonelik Sözleşmesi. QR menü son-müşterisini
    /// kapsayan <see cref="CodeTerms"/>'den ayrıdır: orada Kullanıcı = sipariş
    /// veren müşteri; burada Abone = Platform'un hizmet sattığı işletme.
    /// 6502 sayılı TKHK m.52 + Abonelik Sözleşmeleri Yönetmeliği + 6098 sayılı
    /// TBK karma sözleşme + 6698 sayılı KVKK m.12 veri işleyen yükümlülükleri
    /// + 6563 sayılı E-Ticaret Kanunu çerçevesinde yazılmıştır.
    /// </summary>
    public const string CodePlatformSubscription = "platform_subscription";

    private const string SeedVersion = "2026-06-02";

    public static async Task SeedAsync(AuthDbContext db, CancellationToken ct = default) {
        if (!db.Database.IsRelational()) {
            return;
        }

        var existingCodes = await db.PlatformAgreements
            .IgnoreQueryFilters()
            .Where(a => a.IsActive)
            .Select(a => a.Code)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var defaults = BuildDefaults();
        var toInsert = defaults
            .Where(d => !existingCodes.Contains(d.Code, StringComparer.OrdinalIgnoreCase))
            .Select(d => new PlatformAgreement {
                Id = Ulid.NewUlid(),
                Code = d.Code,
                Title = d.Title,
                BodyMd = d.BodyMd,
                Version = SeedVersion,
                EffectiveFromUtc = now,
                IsActive = true,
                IsRequired = d.IsRequired,
                SortOrder = d.SortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                UpdatedBy = "system:seed"
            })
            .ToList();

        if (toInsert.Count == 0) {
            return;
        }

        db.PlatformAgreements.AddRange(toInsert);
        await db.SaveChangesAsync(ct);
    }

    private record SeedDefinition(string Code, string Title, bool IsRequired, int SortOrder, string BodyMd);

    private static SeedDefinition[] BuildDefaults() => new[] {
        new SeedDefinition(CodeTerms,                "Kullanım Koşulları",                   IsRequired: true,  SortOrder: 10, BodyMd: TermsBody),
        new SeedDefinition(CodeKvkk,                 "KVKK Aydınlatma Metni",                IsRequired: true,  SortOrder: 20, BodyMd: KvkkBody),
        new SeedDefinition(CodeMarketing,            "Ticari Elektronik İleti İzni",         IsRequired: false, SortOrder: 30, BodyMd: MarketingBody),
        new SeedDefinition(CodePlatformSubscription, "Platform Hizmet Abonelik Sözleşmesi",  IsRequired: true,  SortOrder: 5,  BodyMd: PlatformSubscriptionBody)
    };

    // ----------------------------------------------------------------------
    // KULLANIM KOŞULLARI (üyelik sözleşmesi). 6563 sayılı Elektronik Ticaret
    // Kanunu + 6502 sayılı TKHK + 6098 sayılı TBK çerçevesinde mobil SaaS için
    // standart taslak. Şirket adı / adres / iletişim alanları {{token}} ile
    // PlatformIdentity üzerinden gelir — continuo-ops-ui Şirket Bilgileri ekranı.
    // ----------------------------------------------------------------------
    private const string TermsBody = """
# {{agreementTitle}}

Son güncelleme: {{agreementDate}} (versiyon {{agreementVersion}})

## 1. Taraflar ve Tanımlar

İşbu Kullanım Koşulları ve Üyelik Sözleşmesi ("Sözleşme"), **{{companyLegalName}}** ("Platform") ile platforma elektronik ortamda üye olan gerçek ya da tüzel kişi ("Kullanıcı") arasında akdedilmiştir. Sözleşme, Kullanıcı'nın kayıt sırasında onay vermesi ile yürürlüğe girer.

**Platform**: {{companyName}} markası altında sunulan QR menü, sipariş, ödeme, müşteri sadakat ve ilgili dijital hizmetlerin tamamı.

**Hizmet**: Platform üzerinden Kullanıcı'ya sunulan dijital menü görüntüleme, sipariş oluşturma, ödeme, sadakat puanı ve kampanya bildirim hizmetlerinin tamamı.

## 2. Üyelik

2.1. Üyelik, kayıt formundaki bilgilerin Kullanıcı tarafından doğru ve eksiksiz beyan edilmesi ile başlar. Hatalı veya eksik beyandan doğan tüm sorumluluk Kullanıcı'ya aittir.

2.2. Kullanıcı, en az 18 yaşında olduğunu ya da yasal vasisinin onayı ile üye olduğunu beyan eder.

2.3. Kullanıcı hesap bilgilerini (şifre, OTP, oturum açma bilgileri) üçüncü kişilerle paylaşmamayı ve hesabın güvenliğinden sorumlu olduğunu kabul eder.

2.4. Hesap kullanım kurallarına aykırı bir durum tespit edildiğinde Kullanıcı en kısa sürede Platform'a bildirim yapar.

## 3. Hizmetin Kapsamı ve Sınırları

3.1. Platform, hizmetin sürekli ve kesintisiz sunulması için makul çabayı gösterir; ancak teknik bakım, altyapı sağlayıcı kesintileri veya mücbir sebepler nedeniyle hizmette geçici aksamalar yaşanabilir.

3.2. Platform, hizmet kapsamını, özelliklerini ve fiyatlandırmasını önceden bildirimde bulunmak kaydıyla değiştirme hakkını saklı tutar.

3.3. Kullanıcı, Platform üzerinden verdiği siparişlerin doğruluğundan ve seçtiği ödeme yönteminin geçerliliğinden bizzat sorumludur.

## 4. Ödeme ve Cayma

4.1. Sipariş ödemeleri, Platform üzerinde entegre çalışan ödeme sağlayıcıları (örn. iyzico) aracılığıyla tahsil edilir. Kart bilgileri Platform sunucularında saklanmaz.

4.2. Hazır gıda ve içecek siparişleri, 6502 sayılı Tüketicinin Korunması Hakkında Kanun ile Mesafeli Sözleşmeler Yönetmeliği m.15/1-(ç) uyarınca cayma hakkı kapsamı dışındadır.

4.3. Hatalı, eksik veya hasarlı teslimat durumunda Kullanıcı, hizmeti veren işletmeye doğrudan başvuru yapar; Platform aracılık eden konumdadır.

## 5. Fikri Mülkiyet

Platform üzerindeki tüm yazılım, marka, logo, içerik ve tasarımlar {{companyLegalName}} ya da lisans verenlere aittir. Kullanıcı, bu içeriği önceden yazılı izin almaksızın kopyalayamaz, çoğaltamaz, dağıtamaz veya türev eserler oluşturamaz.

## 6. Sorumluluk Sınırı

6.1. Platform, hizmet kullanımı sırasında doğabilecek dolaylı, sonuç olarak ortaya çıkan veya kâr kaybı niteliğindeki zararlardan sorumlu tutulamaz.

6.2. Hizmetin işletme tarafından sunulan menü, fiyat, alerjen ve hijyen bilgilerinin doğruluğundan ilgili işletme sorumludur; Platform aracılık eden konumdadır.

## 7. Hesabın Askıya Alınması ve Feshi

7.1. Kullanıcı dilediği zaman profil ekranından hesabını silebilir; mevzuat gereği saklanması zorunlu kayıtlar yasal süre boyunca saklanır.

7.2. Platform, Sözleşme'ye aykırı davranan ya da hizmeti kötüye kullanan hesapları önceden bildirimde bulunmaksızın askıya alabilir veya feshedebilir.

## 8. Sözleşme Değişiklikleri

Platform, işbu Sözleşme'yi tek taraflı olarak değiştirme hakkını saklı tutar. Değişiklikler Platform içinde duyurulur ve Kullanıcı'nın bir sonraki girişinde onayına sunulur. Kullanıcı değişiklikleri onaylamaması halinde Platform'u kullanmaya devam edemez.

## 9. Uygulanacak Hukuk ve Yetkili Mahkeme

İşbu Sözleşme'den doğan uyuşmazlıklarda Türk Hukuku uygulanır. Yetkili mahkeme ve icra daireleri **{{jurisdictionCity}}** Mahkemeleri ve İcra Daireleridir. Tüketici uyuşmazlıklarında 6502 sayılı Kanun'un Tüketici Hakem Heyetlerine ilişkin parasal sınırları saklıdır.

## 10. İletişim

Sözleşme ile ilgili her türlü bildirim için: **{{companyEmail}}** (KEP: {{companyKep}})
""";

    // ----------------------------------------------------------------------
    // KVKK AYDINLATMA METNI (6698 sayılı Kanun m.10 + Aydınlatma
    // Yükümlülüğünün Yerine Getirilmesi Tebliği). Veri sorumlusu, işleme
    // amaçları, hukuki sebep, aktarım, saklama süresi ve ilgili kişi hakları
    // — tüm zorunlu bölümler dolu.
    // ----------------------------------------------------------------------
    private const string KvkkBody = """
# {{agreementTitle}}

Son güncelleme: {{agreementDate}} (versiyon {{agreementVersion}})

## 1. Veri Sorumlusu

6698 sayılı Kişisel Verilerin Korunması Kanunu ("KVKK") uyarınca veri sorumlusu sıfatıyla **{{companyLegalName}}** (bundan sonra "Platform" olarak anılacaktır) tarafından, üyelik ve hizmet süreçleri kapsamında işlenen kişisel verileriniz hakkında sizi bilgilendirmek isteriz.

Adres: {{companyAddress}}
İletişim: **{{companyEmail}}** — KEP: **{{companyKep}}** — Telefon: {{companyPhone}}

## 2. İşlenen Kişisel Veriler

Platform'a üye olduğunuzda ve hizmeti kullanırken aşağıdaki kişisel veri kategorileri işlenmektedir:

- **Kimlik bilgileri**: ad, soyad, doğum tarihi
- **İletişim bilgileri**: e-posta adresi, cep telefonu numarası, açık adres
- **Müşteri işlem bilgileri**: sipariş geçmişi, masa/şube bilgisi, ödenen tutar, ödeme yöntemi (kart bilgileri saklanmaz, ödeme sağlayıcısında tutulur)
- **İşlem güvenliği bilgileri**: oturum açma kayıtları, IP adresi, cihaz parmak izi, hesap koruma OTP'leri
- **Lokasyon bilgileri** (yalnızca kullanıcı izni ile): en yakın şube önerisi için
- **Pazarlama bilgileri** (yalnızca açık rıza halinde): tercih, kampanya etkileşim geçmişi

## 3. Kişisel Verilerin İşlenme Amacı

Kişisel verileriniz aşağıdaki amaçlarla işlenmektedir:

1. Üyelik kaydının oluşturulması ve hesap güvenliğinin sağlanması
2. QR menü, sipariş alma, ödeme aracılığı ve teslimat hizmetlerinin sunulması
3. Mesafeli satış sözleşmesinin yönetimi, fatura/fiş düzenlenmesi
4. Müşteri talep ve şikayetlerinin karşılanması
5. Sadakat programı ve {{companyName}} Coin bakiyesinin yönetilmesi
6. Hizmet kalitesinin ölçülmesi, kullanım analizleri ve istatistik üretimi
7. Yasal yükümlülüklerin yerine getirilmesi (vergi, ticari kayıt, KVKK Saklama Süreleri)
8. Açık rıza vermeniz halinde tanıtım, kampanya ve duyuru iletişimi

## 4. İşlemenin Hukuki Sebebi

Kişisel verileriniz KVKK m.5 kapsamında aşağıdaki hukuki sebeplere dayanılarak işlenmektedir:

- **Sözleşmenin kurulması ve ifası** (m.5/2-c): üyelik, sipariş, ödeme süreçleri
- **Hukuki yükümlülüğün yerine getirilmesi** (m.5/2-ç): vergi mevzuatı, 6502 sayılı TKHK, KVKK
- **Bir hakkın tesisi, kullanılması veya korunması** (m.5/2-e): uyuşmazlık çözümü, hile/kötüye kullanım tespiti
- **Meşru menfaat** (m.5/2-f): hizmet kalitesinin geliştirilmesi, sistem güvenliği
- **Açık rıza** (m.5/1): tanıtım iletişimi, lokasyon tabanlı şube önerisi

## 5. Kişisel Verilerin Aktarımı

Verileriniz, aşağıdaki üçüncü kişilere KVKK m.8 ve m.9 çerçevesinde aktarılmaktadır:

- **Hizmet aldığınız işletme** (Platform müşterisi olan kafe/restoran): siparişin işletilmesi için
- **Ödeme hizmet sağlayıcısı** (iyzico vb.): ödeme tahsilatı için, BDDK lisansı altında
- **Bulut altyapı sağlayıcıları**: barındırma ve yedekleme için
- **İleti yönetim hizmeti sağlayıcıları (İYS)**: ticari iletişim onayı durumunda
- **Yetkili kamu kurum ve kuruluşları**: yasal talep halinde

Yurt dışına aktarım söz konusu olduğunda KVKK m.9 kapsamında ek koruyucu önlemler alınır ve açık rızanız temin edilir.

## 6. Saklama Süresi

- Üyelik verileri: hesap aktif olduğu sürece + üyelik feshi sonrası 10 yıl (TBK m.146 zamanaşımı süresi)
- Sipariş ve fatura kayıtları: 10 yıl (VUK m.253)
- Pazarlama izni kayıtları: izin geri alınıncaya kadar + 3 yıl (İYS Yönetmeliği)
- IP / log kayıtları: 2 yıl (5651 sayılı Kanun)

## 7. İlgili Kişi Hakları (KVKK m.11)

Kişisel verileriniz hakkında aşağıdaki haklara sahipsiniz:

- Verilerinizin işlenip işlenmediğini öğrenme
- İşlenmişse buna ilişkin bilgi talep etme
- İşleme amacını ve amacına uygun kullanılıp kullanılmadığını öğrenme
- Yurt içinde veya yurt dışında aktarıldığı üçüncü kişileri bilme
- Eksik veya yanlış işlenmişse düzeltilmesini isteme
- KVKK m.7'de öngörülen şartlar çerçevesinde silinmesini veya yok edilmesini isteme
- Yapılan işlemlerin verilerinizin aktarıldığı üçüncü kişilere bildirilmesini isteme
- Otomatik sistemlerle analiz sonucu aleyhinize bir sonuç çıkmasına itiraz etme
- Kanuna aykırı işleme nedeniyle zarara uğramanız halinde tazminat talep etme

Bu haklarınızı kullanmak için **{{companyEmail}}** adresine e-posta gönderebilir veya **{{companyKep}}** KEP adresi üzerinden başvurabilirsiniz. Başvurular Veri Sorumlusuna Başvuru Usul ve Esasları Hakkında Tebliğ kapsamında 30 gün içinde yanıtlanır.
""";

    // ----------------------------------------------------------------------
    // TICARI ELEKTRONIK ILETI IZNI (6563 sayılı Kanun + Ticari İletişim
    // Yönetmeliği + İYS zorunluluğu). Açık rıza metni — opsiyonel.
    // ----------------------------------------------------------------------
    private const string MarketingBody = """
# {{agreementTitle}}

Son güncelleme: {{agreementDate}} (versiyon {{agreementVersion}})

## 1. Kapsam

6563 sayılı Elektronik Ticaretin Düzenlenmesi Hakkında Kanun ve Ticari İletişim ve Ticari Elektronik İletiler Hakkında Yönetmelik uyarınca, **{{companyLegalName}}** ("Platform") tarafından kişisel iletişim adreslerinize tanıtım, kampanya, indirim, kişiselleştirilmiş öneri ve yeni ürün/hizmet duyurularını içeren ticari elektronik ileti gönderilebilmesi için açık rızanıza ihtiyaç duymaktayız.

## 2. İletişim Kanalları

İzin vermeniz halinde size aşağıdaki kanallar üzerinden ticari elektronik ileti gönderilebilecektir:

- **E-posta** (kayıtlı e-posta adresinize)
- **SMS** (kayıtlı cep telefonu numaranıza)
- **Anlık bildirim** (uygulama push notification)
- **WhatsApp / sesli arama** (yalnızca açıkça onayladığınız kanallar için)

## 3. İçerik Türleri

Tarafınıza gönderilebilecek iletiler aşağıdaki içerikleri içerebilir:

- Yeni şube, ürün ve menü duyuruları
- Şahsınıza özel indirim ve kampanyalar
- Sadakat programı ve {{companyName}} Coin kazanım fırsatları
- Lokasyon ve tercihlerinize dayalı kişiselleştirilmiş öneriler
- Anket, geri bildirim ve memnuniyet talepleri

## 4. İzin Yönetimi ve Geri Alma Hakkı

4.1. İzniniz **İleti Yönetim Sistemi (İYS)** üzerinden de kayıt altına alınır ve istediğiniz an İYS web sitesi (iys.org.tr) veya çağrı merkezi üzerinden iznininizi yönetebilirsiniz.

4.2. Aldığınız her e-posta ve SMS'de bulunan "İletişim tercihimi güncelle" / "RED" bağlantısı ile dilediğiniz an, gerekçe göstermeksizin, ücretsiz olarak izninizi geri alabilirsiniz.

4.3. Profilim → Bildirim Tercihleri ekranından kanal ve içerik bazlı kontrolünüzü sağlayabilirsiniz.

4.4. İzin geri alındığı andan itibaren, mevzuat gereği zorunlu işlem ve onay bildirimleri dışında, ticari elektronik ileti gönderimi durdurulur.

## 5. Önemli Not

- Bu izin **opsiyoneldir**. Reddetmeniz halinde hizmetlerden faydalanmanız hiçbir şekilde etkilenmez.
- Sipariş onayı, ödeme tahsilatı, hesap güvenliği ve yasal yükümlülük kapsamındaki bildirimler ticari elektronik ileti kapsamında değildir ve izin durumunuzdan bağımsız olarak gönderilir.

## 6. Veri Sorumlusu

İzin yönetimi ve geri alma talepleriniz için: **{{companyEmail}}** (KEP: {{companyKep}})

İYS Hizmet Sağlayıcı bilgilerimiz İYS portalı üzerinden sorgulanabilir.
""";

    // ----------------------------------------------------------------------
    // PLATFORM HİZMET ABONELİK SÖZLEŞMESİ (Tenant signup).
    //   • Taraflar: Platform Sağlayıcı (Continuo) ↔ Abone (kafe/restoran).
    //   • Tip: Karma sözleşme — istisna + vekâlet + lisans (TBK 6098 m.470 vd.).
    //   • Tüketici unsuru kalkıyor: Abone tacir/esnaf — TKHK m.3/k tüketici
    //     tanımı dışında; ancak Abonelik Sözleşmeleri Yönetmeliği m.5,7,22,23
    //     uygulanır (Yönetmelik m.4/3 elektronik haberleşme dışı abonelik).
    //   • KVKK m.12: Abone Veri Sorumlusu, Platform Veri İşleyen — son müşteri
    //     verisi (QR menü siparişleri, sadakat, masa) Abone adına işlenir.
    //   • 6563 Sayılı E-Ticaret Kanunu: Platform = aracı hizmet sağlayıcı.
    //   • SLA, IP/lisans, gizlilik, sorumluluk sınırı, sona erme + veri ihracı.
    // ----------------------------------------------------------------------
    private const string PlatformSubscriptionBody = """
# {{agreementTitle}}

Son güncelleme: {{agreementDate}} (versiyon {{agreementVersion}})

> Bu sözleşme, **işletme aboneliği** için **{{companyName}}** Platformu'nu kullanacak işletmelerle yapılır. QR menüden sipariş veren son müşteri ile yapılan **Kullanım Koşulları** ve **KVKK Aydınlatma Metni** ayrıdır ve onlar bu sözleşmenin yerine geçmez.

## 1. Taraflar

İşbu Platform Hizmet Abonelik Sözleşmesi ("Sözleşme"), bir tarafta;

**Platform Sağlayıcı**: **{{companyLegalName}}** — adres: {{companyAddress}} — e-posta: {{companyEmail}} — KEP: {{companyKep}} — telefon: {{companyPhone}} (bundan sonra "**Platform**" olarak anılacaktır)

diğer tarafta;

**Abone**: Kayıt formunda beyan ettiği unvan, vergi kimlik numarası, MERSİS numarası ve iletişim bilgileriyle Platform'a kayıt olan gerçek veya tüzel kişi tacir / esnaf (bundan sonra "**Abone**" olarak anılacaktır)

arasında elektronik ortamda akdedilmiştir. Sözleşme, Abone'nin kayıt formunu doldurup işbu metni elektronik onayla kabul ettiği anda yürürlüğe girer ve kabul tarihiyle imzalı PDF formunda Abone'nin beyan ettiği e-posta adresine gönderilir.

## 2. Tanımlar

- **Hizmet**: Platform'un teknik altyapısı üzerinden Abone'ye sunulan QR menü, sipariş yönetimi, ödeme aracılığı, müşteri sadakat, raporlama, stok-mutfak ekran, robotik orkestrasyon, yapay zeka asistan ve bağlantılı modüllerin tamamı.
- **Paket**: Abone'nin kayıt formunda seçtiği abonelik paketi (örn. Başlangıç, Profesyonel, İşletme, Kurumsal) ve ona bağlı modül / kullanıcı / şube / token kotaları.
- **Ek Modül**: Pakete ek olarak ücretli alınan modüller.
- **Tenant**: Abone'ye özel oluşturulan mantıksal alan; subdomain (`{slug}.{{companyWebsite}}`), veri kümesi, kullanıcı listesi, ayarlar bu alan kapsamındadır.
- **Son Müşteri Verisi**: Abone'nin şubelerinden Platform üzerinden sipariş veren / sadakat üyesi olan gerçek kişilerin verileri.
- **Aşağıda 'Kayıt Beyanı'** denildiğinde Abone'nin kayıt formunda doldurduğu unvan, adres, iletişim, paket, marka kimliği ve KVKK seçimleri kastedilir.

## 3. Sözleşmenin Konusu

3.1. Platform, Abone'ye seçtiği Paket kapsamındaki Hizmet'i, işbu Sözleşme süresi boyunca uzaktan erişimli SaaS modelinde sunmayı; Abone ise seçtiği Paket bedelini ve seçtiği Ek Modül bedellerini ödemeyi karşılıklı kabul eder.

3.2. Sözleşme **belirsiz süreli abonelik** sözleşmesi niteliğindedir. Aylık veya yıllık ödeme dönemi tercih edilebilir; ödeme dönemi otomatik yenilenir.

3.3. Sözleşme; **6098 sayılı Türk Borçlar Kanunu** (karma sözleşme), **6502 sayılı Tüketicinin Korunması Hakkında Kanun** ve **Abonelik Sözleşmeleri Yönetmeliği** (özellikle m.5, 6, 7, 8, 13, 22-25), **6698 sayılı KVKK**, **6563 sayılı Elektronik Ticaretin Düzenlenmesi Hakkında Kanun**, **5651 sayılı İnternet Kanunu** ve **5846 sayılı FSEK** çerçevesinde yorumlanır.

## 4. Kayıt Beyanı'nda Kullanılan Bilgiler

Abone, kayıt sırasında aşağıdaki bilgileri Platform'a sağladığını, doğru ve güncel olduklarını, sorumluluğun kendisinde olduğunu beyan ve kabul eder:

- **İşletme kimliği**: ticari unvan, MERSİS / vergi kimlik no, adres, faaliyet sektörü
- **İletişim**: yetkili kişi ad-soyad, iletişim e-postası, telefon, KEP (varsa)
- **Marka kimliği**: işletme adı, slogan, logo, marka renkleri, font tercihi
- **Operasyonel tercih**: dil, saat dilimi, para birimi, şube/kullanıcı sayısı planı
- **KVKK seçimi**: aydınlatma yükümlülüğünün ifa edileceği yöntem onayı
- **Doğrulama**: e-posta üzerinden 2FA OTP doğrulaması, IP adresi, tarayıcı parmak izi, kayıt zamanı

Bu bilgiler **{{companyName}}** sistemleri tarafından (a) tenant oluşturma, (b) faturalama ve ödeme aracılığı, (c) marka temalı QR menü / sipariş arayüzü oluşturma, (d) destek-iletişim kanalı kurma, (e) güvenlik kontrolleri ve (f) yasal yükümlülükler için işlenir. Detaylı işleme amaçları işbu Sözleşme'nin Madde 9'unda düzenlenmiştir.

## 5. Ücret, Ödeme ve Faturalama

5.1. Aylık veya yıllık paket ücreti **kayıt formunda gösterilen tutar üzerinden** Abone tarafından Platform'a ödenir. Fiyatlar KDV hariçtir; Türkiye'de mukim Abone faturalarına **%20 KDV** eklenir.

5.2. Ödeme; (a) yurt içi kredi/banka kartı (PCI-DSS sertifikalı **iyzico** veya muadili ödeme kuruluşu), (b) IBAN havale + QR referans kodu veya (c) Platform tarafından izin verilen diğer yöntemler ile yapılabilir. Kart bilgileri Platform sunucularında saklanmaz; tokenize formda ödeme kuruluşunda muhafaza edilir.

5.3. Aylık ödemeli aboneliklerde fatura her ay yenileme tarihinde, yıllık aboneliklerde dönem başında düzenlenir. **e-Arşiv / e-Fatura** olarak Abone'nin beyan ettiği yetkili e-posta adresine iletilir.

5.4. Ücret değişiklikleri **en az 30 gün öncesinden** Abone'ye e-posta ile bildirilir. Yeni ücret bir sonraki yenileme tarihinden itibaren geçerlidir. Abone yeni ücreti kabul etmezse Madde 14 kapsamında sözleşmeyi feshedebilir.

5.5. Faturanın muaccel olduğu tarihten itibaren **14 (ondört) gün** içinde ödenmeyen abonelikler için Platform aşağıdaki adımları sırasıyla uygulayabilir: e-posta hatırlatma → hesap işlevsel kısıtlama (yeni sipariş alma duraklatma) → **30 (otuz) gün** süreyle hesap askıya alma → veri ihracı sonrası kalıcı kapatma.

5.6. **6502 sayılı TKHK m.52/4 ve Abonelik Yönetmeliği m.22**: Abone, belirsiz süreli abonelik sözleşmesini herhangi bir gerekçe göstermeksizin ve cezai şart ödemeksizin istediği zaman feshedebilir. Belirli süreli (yıllık) sözleşmenin süresi bir yıldan uzunsa da aynı hak geçerlidir.

## 6. Hizmet Kapsamı ve Hizmet Seviyesi (SLA)

6.1. **Kullanılabilirlik hedefi**: Aylık ortalama **%99.5 uptime** (planlı bakım pencereleri hariç). Erişilemezlik durumunda Platform makul süre içinde Abone'ye e-posta üzerinden bilgi verir.

6.2. **Planlı bakım**: Mümkün olduğunda yerel saatle **02:00 — 06:00** arasında yapılır ve en az 24 saat önceden duyurulur.

6.3. **Destek kanalı**: Destek talepleri **{{companyEmail}}** veya panel içi destek arayüzü üzerinden iletilir. Kritik olaylarda en geç **4 saat içinde** ilk yanıt verilir.

6.4. Platform, hizmetin kapsamını, mimari unsurlarını ve modül içeriğini önceden bildirimde bulunmak kaydıyla değiştirme hakkını saklı tutar; önemli ölçüde işlevsellik kaybı doğuran değişiklikler için Abone'ye Madde 14 kapsamında fesih hakkı tanınır.

6.5. **Mücbir sebep**: Doğal afet, salgın, savaş, siber saldırı, yetkili kamu otoritesi kararı, üst düzey altyapı sağlayıcı kesintisi gibi tarafların kontrolü dışındaki olaylar nedeniyle yaşanan hizmet kesintileri Platform'a sorumluluk yüklemez.

## 7. Abone'nin Yükümlülükleri

7.1. Abone; Platform'u (a) yürürlükteki Türk mevzuatına, (b) ahlaka ve kamu düzenine, (c) üçüncü kişi haklarına aykırı biçimde kullanmamayı kabul eder.

7.2. Abone, Platform üzerinden yüklediği menü, fiyat, alerjen, hijyen, ürün görseli, kampanya metni ve fatura bilgilerinin doğruluğundan münhasıran kendisi sorumludur. Platform bu içerikler bakımından **Aracı Hizmet Sağlayıcı** konumundadır (6563 sayılı Kanun).

7.3. Abone, Platform üzerinden tahsil ettiği bedelin son müşteriye fatura/fiş kesilmesinden, e-fatura/e-arşiv mevzuatından, **POS yazarkasa entegrasyonundan** ve KDV beyanlarından sorumludur.

7.4. Abone kendi panel kullanıcılarına (çalışan, müdür, sahip) verdiği yetkilerin uygunluğunu sağlar; bu kullanıcıların eylem ve ihmallerinden Abone sorumludur.

7.5. Abone, Platform'u **otomatik sistemler ya da botlar** aracılığıyla aşırı yüke sokmayacağını, tersine mühendislik yapmayacağını ve Platform yazılımını izinsiz çoğaltıp/dağıtmayacağını kabul eder.

## 8. Hesap Güvenliği

8.1. Abone'nin yetkili kullanıcıları; parola, OTP kodu, oturum çerezi ve API anahtarlarını üçüncü kişilerle paylaşmamayı kabul eder. Hesap erişim bilgilerinin güvenliği Abone'nin sorumluluğundadır.

8.2. Şüpheli erişim ya da yetkisiz işlem tespit edildiğinde Abone, durumu Platform'a ivedilikle bildirir; Platform makul süre içinde hesabı koruma altına alır.

8.3. Platform; güvenlik tehdidi gördüğü hesapları **önceden bildirim yapma yükümlülüğü olmaksızın** geçici olarak askıya alabilir, koruyucu önlem aldıktan sonra Abone'yi bilgilendirir.

## 9. Kişisel Verilerin Korunması (KVKK)

9.1. **Veri Sorumlusu / Veri İşleyen ayrımı**:
- Abone'nin kendi çalışanlarına, müşterilerine ve potansiyel müşterilerine ait kişisel veriler bakımından **Abone, Veri Sorumlusu** sıfatını taşır.
- Platform, Abone adına bu verileri işleyen **Veri İşleyen** sıfatını taşır (6698 sayılı KVKK m.12/2).

9.2. Abone'nin kayıt formunda doldurduğu **kendi yetkili kişisinin** kişisel verileri (ad, e-posta, telefon vb.) bakımından **Platform, Veri Sorumlusu** sıfatını taşır ve bu veriler işbu Sözleşme'nin ifası amacıyla işlenir.

9.3. Platform; Abone adına işlediği verileri:
- yalnızca Abone'nin yazılı talimatları çerçevesinde işler,
- yetkisiz erişime karşı **şifreleme**, **erişim kontrolü**, **kayıt tutma**, **periyodik penetrasyon testi** dahil olmak üzere makul teknik ve idari tedbirleri alır,
- alt-işleyen (cloud sağlayıcısı, yedekleme servisi, OCR/AI sağlayıcı) kullanırken **eşdeğer koruma standartları** içeren yazılı sözleşme akdeder,
- KVKK m.12/5 kapsamında **veri ihlali** tespit edildiğinde en geç **72 saat içinde** Abone'ye yazılı bildirim yapar.

9.4. **Sınır ötesi aktarım**: Platform'un kullandığı altyapı bileşenlerinin bir kısmı yurt dışında konumlu olabilir. Yurt dışına aktarım, **KVKK m.9** (10.07.2024 değişikliği sonrası) çerçevesinde Kurul'un yeterli koruma kararına ya da Standart Sözleşme'ye dayanılarak yapılır; ek koruyucu önlemler işbu Sözleşme'nin ekidir.

9.5. **Aydınlatma yükümlülüğü**: Abone, kendi son müşterilerine yönelik KVKK aydınlatma metnini ve açık rıza beyanını **kendi sorumluluğunda** hazırlamayı kabul eder. Platform, Abone'nin tenant alanında özelleştirilebilir aydınlatma şablonları sunabilir; ancak bu metinlerin hukuki uygunluğu Abone'ye aittir.

9.6. **Veri ihracı**: Sözleşme'nin sona ermesi halinde Abone'nin verisi **30 gün** süreyle ihraç edilebilir biçimde saklanır; bu süre sonunda Platform verileri silmekle yükümlüdür. Yedek arşivleme rotasyonu nedeniyle silme **90 günü** geçmeyecek şekilde tamamlanır.

## 10. Fikri Mülkiyet

10.1. Platform yazılımı, kaynak kodu, veritabanı şeması, marka, logo, tasarım, dokümantasyon ve tüm türev eserleri üzerinde haklar **{{companyLegalName}}**'a aittir. İşbu Sözleşme Abone'ye yalnızca **sözleşme süresince geçerli, münhasır olmayan, devredilemez kullanım lisansı** verir.

10.2. Abone'nin Platform'a yüklediği logo, görsel, menü ve içerikler bakımından mülkiyet **Abone'de** kalır; Abone, bu içerikleri Platform'un Hizmet'i sunması için gerekli ölçüde **kullanma, çoğaltma, görüntüleme ve son müşteriye sunma** lisansını Platform'a verir. Lisans, sözleşme sona erince son bulur.

10.3. Abone'nin Platform üzerinde ürettiği veriler (sipariş geçmişi, raporlar, anonim agrega istatistikler) Abone'ye aittir. Platform; anonim ve kişisel veri içermeyen agrega istatistikleri **ürün geliştirme** amacıyla saklı tutar.

## 11. Gizlilik

11.1. Taraflar, Sözleşme süresince birbirleri hakkında öğrendikleri **ticari sır**, **müşteri listesi**, **fiyatlandırma**, **strateji**, **teknik mimari** bilgilerini Sözleşme'nin sona ermesinden sonra **5 yıl** süreyle gizli tutmayı kabul eder.

11.2. Gizlilik yükümlülüğü; (a) tarafların kontrolünden bağımsız olarak kamuya açık hale gelmiş bilgileri, (b) yasal merciin yazılı talebine dayalı paylaşımı, (c) tarafların yetkili çalışanları / hukuk danışmanları arası paylaşımı kapsamaz.

## 12. Sorumluluk Sınırı

12.1. Platform; **dolaylı zararlar**, **kâr kaybı**, **iş kaybı**, **müşteri kaybı**, **veri kaybı** (yedekleme yükümlülüğü dışında), **itibar kaybı** ve sair sonuç olarak ortaya çıkan zararlardan sorumlu değildir.

12.2. Platform'un her bir Abone'ye karşı toplam sorumluluğu; **olaydan önceki 12 (oniki) ayda** Abone'den tahsil ettiği abonelik bedeli toplamı ile sınırlıdır.

12.3. Bu sınırlama; **Platform'un kasıt veya ağır ihmali** sonucu doğan zararlarda, **kişisel veri ihlali** kapsamındaki KVKK Kurulu idari para cezaları ve **fikri mülkiyet ihlali** durumunda uygulanmaz.

12.4. Abone, Platform üzerinden işlettiği menü-fiyat-alerjen bilgilerinin yanlışlığından, kendi son müşterilerine karşı KVKK yükümlülüğünü ihmalinden, yetkisiz panel kullanıcısının eylemlerinden doğan zararlardan tek başına sorumludur.

## 13. Mücbir Sebep

Sel, deprem, yangın, salgın hastalık, savaş, terör, grev, lokavt, yetkili kamu otoritesi kararları, dağıtık hizmet engelleme (DDoS) saldırıları, bulut altyapı sağlayıcılarının küresel kesintileri ve tarafların makul kontrolü dışındaki benzer olaylar mücbir sebep sayılır. Mücbir sebep süresince yükümlülükler durur; sebep **30 günü** aşarsa taraflar Sözleşme'yi feshedebilir.

## 14. Sözleşmenin Sona Ermesi

14.1. **Abone tarafından fesih**: Abone, panel içi "Aboneliği Sonlandır" işlevi veya Platform'a yazılı bildirim ile sözleşmeyi her zaman feshedebilir. Aylık abonelikte cari dönem sonu, yıllık abonelikte cari yıl sonu itibarıyla yürürlüğe girer; **TKHK m.52 hakları saklıdır**.

14.2. **Platform tarafından fesih**: Aşağıdaki hallerden birinin gerçekleşmesi durumunda Platform; 14 gün önceden yazılı bildirim koşuluyla Sözleşme'yi feshedebilir:
- Madde 5.5 kapsamında ödeme aksaması 30 günü aşması
- Madde 7'deki yükümlülüklere maddi ihlal
- Hesabın hile, kara para aklama, terör finansmanı, çocuk istismarı vb. yasa dışı amaçlarla kullanılmasının tespiti (bu halde **bildirim süresi olmadan** derhal fesih)
- Yetkili kamu kurumu kararı

14.3. **Veri ihracı**: Sözleşme'nin her halükarda sona ermesinde Abone, **30 gün** süreyle Platform üzerinden tam veri ihracı (sipariş geçmişi, müşteri listesi, ayarlar, medya) talebinde bulunabilir; talep CSV / JSON / PDF arşiv formatında karşılanır. Sürenin sonunda Madde 9.6 kapsamında veriler silinir.

14.4. **İade**: Yıllık ödenmiş aboneliklerde Abone'nin haklı sebep göstermeden fesih halinde **kalan tam aylar** için orantılı iade yapılır; ödeme komisyonları iadeden düşülür. Aylık ödemelerde cari dönem için iade yapılmaz.

## 15. Sözleşmedeki Değişiklikler

15.1. Platform işbu Sözleşme'yi değiştirme hakkını saklı tutar. **Önemli** değişiklikler **en az 30 gün öncesinden** Abone'ye e-posta ile bildirilir; sonraki giriş ekranında yeni metin Abone'nin onayına sunulur.

15.2. Abone değişiklikleri kabul etmezse Madde 14.1 kapsamında sözleşmeyi feshedebilir. Onaylamadan Hizmet'i kullanmaya devam etmek mümkün değildir.

15.3. **Önemsiz** (yazım hatası düzeltme, iletişim bilgisi güncelleme vb.) değişiklikler ek onay aranmaksızın yürürlüğe girer; Abone panel duyuruları üzerinden bilgilendirilir.

## 16. Tebligat

16.1. Tarafların Sözleşme'de yer alan **e-posta** ve (varsa) **KEP** adresleri tebligat adresi olarak kabul edilir. Bu adreslere yapılan tebligat geçerli sayılır.

16.2. Adres değişiklikleri **5 iş günü** içinde diğer tarafa bildirilmedikçe eski adrese yapılan tebligat geçerli sayılır.

## 17. Uygulanacak Hukuk ve Yetkili Mahkeme

17.1. İşbu Sözleşme **Türk Hukuku**'na tabidir.

17.2. Sözleşme'den doğan uyuşmazlıkların çözümünde **{{jurisdictionCity}} Mahkemeleri ve İcra Daireleri** yetkilidir. Abone tacir olmasa dahi (6098 TBK m.6 esnaf hükümleri saklıdır) bu yetki kuralı geçerlidir.

## 18. Bütünlük ve Geçersizlik

18.1. İşbu Sözleşme, ekleri ile birlikte taraflar arasında tam mutabakatı oluşturur ve önceki sözlü/yazılı tüm beyanların yerine geçer.

18.2. Sözleşme'nin herhangi bir hükmünün geçersiz / uygulanamaz hale gelmesi diğer hükümlerin geçerliliğini etkilemez. Geçersiz hüküm, **tarafların ortak iradesine en yakın geçerli hüküm** ile değiştirilmiş sayılır.

## 19. Elektronik Onay ve İmzalı PDF Arşivi

19.1. Abone'nin kayıt akışında 2FA doğrulamasından sonra "Onaylıyorum" butonuna tıklaması işbu Sözleşme'nin **elektronik kabul** olarak yürürlüğe girmesini sağlar.

19.2. Onay anında Sözleşme'nin tam metni, versiyon bilgisi, Abone unvanı, IP adresi, tarayıcı kimliği, onay zamanı (UTC) ve 2FA doğrulama referansı dahil olmak üzere **imzalı PDF** olarak oluşturulur; Platform DMS arşivine kaydedilir ve Abone'nin yetkili e-posta adresine eklenmiş olarak gönderilir.

19.3. İmzalı PDF; uyuşmazlık halinde **delil** olarak kullanılır. Abone, panel içinden geçmiş tüm sözleşme sürümlerine ve kabul kayıtlarına her zaman erişebilir.

## 20. İletişim

Sözleşme ile ilgili her türlü soru, talep ve bildirim için:
- E-posta: **{{companyEmail}}**
- KEP: **{{companyKep}}**
- Telefon: {{companyPhone}}
- Web: {{companyWebsite}}
""";
}

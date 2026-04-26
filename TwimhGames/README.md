# Unity Match Puzzle Case Çözümü

Hedef Unity sürümü: **6000.3.11f1**

Bu depo, grid tabanlı match puzzle çekirdek oynanış döngüsünün temiz, modüler ve mülakat odaklı bir implementasyonunu içerir.

## Kapsam

- Dinamik grid board (`NxM`, varsayılan `6x6`)
- ScriptableObject playable-mask ile opsiyonel şekilli level düzenleri
- Konfigüre edilebilir meyve tile kataloğu ile rastgele tile üretimi (sprite tabanlı)
- Sprite render boyutu hücreye otomatik uyar (200x200 assetler ve farklı PPU değerleri için güvenli)
- Katmanlı board görselleri: arka panel + hücre kareleri + meyve sprite'ları
- Özel taşlar, `BoardConfig -> Visual` altındaki sprite ikonları (Color/Bomb/Lightning) ile meyve görselinin yerine gösterilebilir
- Tile view için object pooling
- Bitişik swap doğrulaması ve geçersiz hamlede geri alma
- Match tespiti (`3+` yatay/dikey)
- Özel taş üretim kuralları:
  - `4`'lü çizgi eşleşmesi -> **Lightning**
  - `5+` çizgi eşleşmesi -> **Color**
  - yatay+dikey kesişim (`T/L/+`) -> **Bomb**
- Özel taş tetik davranışları:
  - **Lightning**: bulunduğu satır + sütunun tamamını temizler
  - **Bomb**: ayarlanan alanı temizler (`3x3`, `5x5` veya custom)
  - **Color**: swap edildiği meyve türündeki tüm taşları temizler
- Özel taş kullanım davranışı:
  - özel taşların tetiklenmesi için artık ayrıca match şartı yok
  - özel taşlar normal 3+ meyve match'lerinde pasif kalır; sadece direkt swap ile veya başka bir özel taşın etki alanına girince tetiklenir
  - özel taş, herhangi bir komşu taşla swap edilirse direkt çalışır
  - özel + özel direkt swap'larda normal tekil tetik yerine daha güçlü combo paterni uygulanır
  - yeni özel taş üretimi yalnızca oyuncunun başlattığı ilk match ile sınırlıdır; cascade ve shuffle temizliği ekstra özel üretmez
- Özel taşlar arası zincir reaksiyon (chain reaction)
- Geçerli hamle kalmazsa board, en az bir geçerli hamle garanti edecek şekilde otomatik shuffle edilir
- Oyuncu bir süre hamle yapmazsa sistem hamle önerisi gösterir
  - öneri sistemi düz match yerine özel taş üreten hamleleri önceliklendirir
- DOTween entegre görsel akış (DOTween define aktifse):
  - öneri animasyonu: büyü/küçül + swap önizlemesi + eski konuma dönüş
  - match temizleme: havuza dönmeden önce hızlı küçülme animasyonu
- Deterministik tam çözüm döngüsü:
  - match
  - clear
  - special trigger
  - drop
  - refill
  - board stabil olana kadar auto-cascade

## Çalıştırma

1. Projeyi Unity `6000.3.11f1` ile açın.
2. `Assets/Scenes/SampleScene.unity` sahnesini açın.
3. Play'e basın.

Sahnede `PuzzleGameInstaller` yoksa `PuzzleGameBootstrap` otomatik oluşturur; bu yüzden manuel kurulum olmadan prototip oynanabilir.

## Şekilli Level Kurulumu (ScriptableObject)

1. `Create > TwimhGames > Puzzle > Level Layouts` ile asset oluşturun.
2. `LevelLayout` inspector içinde:
   - `Levels` bölümünden level ekleyin, kopyalayın, silin veya sıralayın
   - bir level kartına tıklayarak genişletin
   - ilgili level için `Name`, `Width`, `Height` ve tıklanabilir `Layout Grid` alanını düzenleyin
3. Karakterler:
   - oynanabilir: `1`, `X`, `O`, `#`
   - bloklu: `0`, `.`, `-`, `_`, boş
4. Bu asset'i `BoardConfig -> Level Layout` alanına atayın.
5. `Level Layout` atanmışsa board boyutu ve playable mask seçili level'dan gelir; atanmamışsa `BoardConfig` içindeki width/height kullanılır.

## Editor Kolaylıkları

- `LevelLayoutSO` custom inspector özellikleri:
  - accordion tarzı çoklu level düzenleme
  - `Add Level`, `Duplicate`, `Remove`, `Move Up`, `Move Down`
  - hızlı presetler: `Fill`, `Clear`, `Invert`, `Border`, `Cross`, `Diamond`
  - doğrulama uyarıları (playable hücre sayısı, kopuk grup bilgisi)
- tek tık kurulum menüsü:
  - `TwimhGames > Puzzle > Create Default Case Assets`
  - hedef klasör sorar, BoardConfig/TileCatalog/LevelLayout üretip bağlar
  - sabit asset path kullanmadan, enum token isimleriyle sprite arayıp TileCatalog ve özel ikonları doldurur

## Temel Konfigürasyon

Hamle önerisi zamanlaması `BoardConfig -> Timings` altından ayarlanır:
- `HintDelay`
- `HintBlinkInterval`
- `ClearAnimationDuration`
- `NoMoveShuffleDelay`

Board düzeni `BoardConfig` üzerinden ayarlanır:
- `Slot Size`: meyve altındaki kare boyutu
- `Cell Gap`: komşu kareler arasındaki boşluk
- `Fruit Size`: meyve sprite boyutu
- `Visual -> ShowBoardBackground`: board paneli + hücre karelerinin runtime üretimini aç/kapat
- `Bomb Area -> Mode`: `3x3`, `5x5` veya `Custom`
- `Bomb Area -> Custom Width/Height`: `Mode = Custom` iken bombanın grid alan boyutu
- `Special Combos`: `Bomb+Bomb`, `Lightning+Lightning` ve `Bomb+Lightning` combo temizleme güçleri
- `Allow Cascade Special Spawns`: otomatik cascade match'lerinde de özel taş üretimine izin verir
- `Generation -> MaxGenerationAttempts`: geçerli board üretimi için yeniden deneme bütçesi
- `DropDurationPerCell` / `RefillDurationPerCell`: uzun düşüşlerde ek animasyon süresi (drop/refill akıcılığı için ana ayar)
- `DropDurationMax` / `RefillDurationMax`: uzun düşüşlerde süre tavanı (cascade'in fazla uzamamasını sağlar)
- `Camera`: board kadraj padding'i ve oyun kamerası arka plan rengi

DOTween kuruluysa ve `DOTWEEN` veya `DOTWEEN_ENABLED` define'ı aktifse, öneri animasyonu ve clear küçülme animasyonu DOTween üzerinden çalışır.

## Klasör Yapısı

- `Assets/_Project/Scripts/Core`
- `Assets/_Project/Scripts/Grid`
- `Assets/_Project/Scripts/Tiles`
- `Assets/_Project/Scripts/Input`
- `Assets/_Project/Scripts/StateMachine`
- `Assets/_Project/Scripts/Match`
- `Assets/_Project/Scripts/Pooling`
- `Assets/_Project/Scripts/Events`
- `Assets/_Project/Scripts/Config`
- `Assets/_Project/Scripts/Visual`
- `Assets/_Project/ScriptableObjects`
- `Assets/_Project/Prefabs`
- `Assets/_Project/Scenes`
- `Assets/_Project/Art/Placeholders`

## Mimari Tercihler

- **Board orkestrasyonu**
  - BoardManager; board model + view eşlemesini ve üst seviye board operasyonlarını yönetir (spawn, swap, clear, drop, refill, shuffle sunumu).
  - BoardRuntimeValidator; init/reload sonrası board tutarlılığını fail-fast doğrular.
- **Model/View ayrımı**
  - TileModel saf oynanış verisidir.
  - TileView görsel + collider temsilidir.
- **Tek sorumluluklu servisler**
  - MatchFinder: yalnızca match bulur.
  - PlayableBoardGenerator: başlangıçta match üretmeden stabil board üretir ve en az bir geçerli hamleyi garanti eder.
  - SpecialTileResolver: özel taş etki alanını, direkt special swap'ları ve chain reaction genişlemesini yönetir.
  - SwapController: komşuluk kontrolü, swap geçerliliği ve rollback.
  - BoardResolver: resolve loop orkestrasyonu.
  - LevelNavigationController: önceki/sonraki level buton akışını installer'dan izole eder.
  - PuzzleCameraConfigurator: kamera çözümleme/konfigürasyonunu installer'dan izole eder.
- **State machine**
  - GameStateMachine, input ve oynanış akışını açık şekilde state bazlı sınırlar.
- **Event-driven akış**
  - GameEventBus; state/swap/match/special/stable eventlerini yayınlar ve sistemler arası direkt bağımlılığı azaltır.
- **Data-driven konfigürasyon**
  - BoardConfigSO ve TileCatalogSO; board kuralları, generation kısıtları, special-combo değerleri, kamera kadrajı ve tile kataloğunu tanımlar.
  - Asset atanmazsa runtime default oluşturulur; proje temel haliyle yine oynanabilir kalır.

## Kullanılan Pattern'ler (Neden)

- **State Machine** (`GameStateMachine`)
  - Geçersiz eşzamanlı işlemleri ve input zamanlama hatalarını engeller.
- **Observer/Event Bus** (`GameEventBus`)
  - Çekirdek mantığı değiştirmeden UI/analytics/VFX gibi abonelerin eklenmesini kolaylaştırır.
- **Object Pooling** (`TilePoolManager`)
  - Clear/refill cascade sırasında sık instantiate/destroy maliyetini azaltır.
- **Coordinator + Service ayrımı**
  - Tek büyük monolitik script yerine okunabilirlik ve test edilebilirlik sağlar.

## Bilinen Sınırlamalar

- Dolaylı tetiklenen Color special, hâlâ saklanan base fruit türünü kullanır; yalnızca direkt swap tetiklemesi swap yapılan meyve türünü ödünç alır.
- Şekilli boardlarda gravity, sütun bazlı dikey kompaksiyon şeklindedir (yan akış/pathfinding fill yok).
- Input temel düzeydedir (click/tap + drag-swap); gelişmiş gesture/touch UX iyileştirmeleri yoktur.
- Oyun UI'si (score/move/goal), VFX/SFX ve production polish kapsam dışıdır.
- Geçerli hamle üretilemeyen bozuk level mask'leri, sessizce dönmek yerine board generation aşamasında fail-fast durdurulur.

## Daha Fazla Zaman Olsaydı

- Şu başlıklar için unit/integration test eklenirdi:
  - swap legality
  - match detection edge case'leri
  - chain reaction determinism
  - resolve loop invariant'ları
- Play mode'a girmeden imkânsız şekilli layout'ları önden yakalayan statik editor doğrulaması eklenirdi.
- Color special etkileşim varyantları zenginleştirilirdi (ör. direkt swap'ta komşu taş rengini birleştirme).
- Varsayılan ScriptableObject assetlerini otomatik kuran editor tooling genişletilirdi.
- `GameEventBus` aboneli debug HUD eklenirdi.

## Teslim Notları

- Implementasyon, polish yerine doğruluk ve sürdürülebilirliği önceliklendirir.
- Mülakat anlatımı için çekirdek sistemler bilinçli olarak ayrıştırılmıştır:
  - swap validation + rollback
  - match finder izolasyonu
  - playable-board generation
  - special chain expansion
  - deterministik board resolve loop
  - açık state gating
- Placeholder görseller bilinçli olarak sade tutulmuştur; odak mimari ve oynanış doğruluğudur.

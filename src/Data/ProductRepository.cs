using SalesWorkflow.Models;

namespace SalesWorkflow.Data;

public interface IProductRepository
{
    IReadOnlyList<Product> GetAll();
    IReadOnlyList<Product> FindBySkuOrName(string query);
}

public class ProductRepository : IProductRepository
{
    private readonly IReadOnlyList<Product> _products =
    [
        // ── Laptops ────────────────────────────────────────────────────────────
        new()
        {
            Id          = "laptop-001",
            Sku         = "DELL-XPS15-2025",
            Name        = "Dell XPS 15 (2025)",
            Brand       = "Dell",
            Category    = "Laptops",
            Description = "15.6\" OLED display, Intel Core Ultra 9 185H, 32 GB LPDDR5X, 1 TB SSD, NVIDIA RTX 4070. " +
                          "Premium build quality with CNC-machined aluminium chassis. Ideal for creative professionals.",
            Price         = 1_799.99m,
            Currency      = "USD",
            StockQuantity = 12,
            Tags          = ["oled", "intel", "nvidia", "creator", "4k", "slim"]
        },
        new()
        {
            Id          = "laptop-002",
            Sku         = "APPLE-MBP14-M4",
            Name        = "Apple MacBook Pro 14\" M4 Pro",
            Brand       = "Apple",
            Category    = "Laptops",
            Description = "Apple M4 Pro chip, 24 GB unified memory, 512 GB SSD, Liquid Retina XDR display. " +
                          "Up to 22 h battery life. Best-in-class performance per watt for developers and video editors.",
            Price         = 1_999.00m,
            Currency      = "USD",
            StockQuantity = 8,
            Tags          = ["apple", "m4", "retina", "developer", "video-editing", "macos", "battery"]
        },
        new()
        {
            Id          = "laptop-003",
            Sku         = "ASUS-ROG-G14-2025",
            Name        = "ASUS ROG Zephyrus G14 (2025)",
            Brand       = "ASUS",
            Category    = "Laptops",
            Description = "AMD Ryzen AI 9 HX 370, 32 GB DDR5, 1 TB SSD, NVIDIA RTX 4070 Super, 14\" QHD+ 165 Hz. " +
                          "Compact gaming powerhouse with ROG Nebula Display. MUX switch for dedicated GPU mode.",
            Price         = 1_649.99m,
            Currency      = "USD",
            StockQuantity = 5,
            Tags          = ["gaming", "amd", "nvidia", "high-refresh", "compact", "rog"]
        },
        new()
        {
            Id          = "laptop-004",
            Sku         = "MS-SURFL7-15",
            Name        = "Microsoft Surface Laptop 7 15\"",
            Brand       = "Microsoft",
            Category    = "Laptops",
            Description = "Snapdragon X Elite, 16 GB LPDDR5X, 512 GB NVMe SSD, 15\" PixelSense Flow touchscreen. " +
                          "Fanless design, up to 20 h battery, Copilot+ PC with on-device AI features.",
            Price         = 1_399.99m,
            Currency      = "USD",
            StockQuantity = 15,
            Tags          = ["microsoft", "windows", "copilot-plus", "touchscreen", "arm", "fanless", "battery"]
        },
        new()
        {
            Id          = "laptop-005",
            Sku         = "HP-SPEC-X360-14",
            Name        = "HP Spectre x360 14",
            Brand       = "HP",
            Category    = "Laptops",
            Description = "Intel Core Ultra 7 155H, 16 GB LPDDR5, 512 GB SSD, 14\" 2.8K OLED 120 Hz touch display. " +
                          "360° hinge with Active Pen support, up to 17 h battery. Premium 2-in-1 convertible.",
            Price         = 1_549.99m,
            Currency      = "USD",
            StockQuantity = 3,
            Tags          = ["2-in-1", "oled", "touch", "pen", "convertible", "intel", "slim"]
        },

        // ── Phones ─────────────────────────────────────────────────────────────
        new()
        {
            Id          = "phone-001",
            Sku         = "APPLE-IP17P-256",
            Name        = "Apple iPhone 17 Pro 256 GB",
            Brand       = "Apple",
            Category    = "Phones",
            Description = "A19 Pro chip, 6.3\" Super Retina XDR ProMotion display, 48 MP triple camera system with " +
                          "5x optical zoom, titanium frame, Action button, USB-C. iOS 18.",
            Price         = 1_199.00m,
            Currency      = "USD",
            StockQuantity = 20,
            Tags          = ["apple", "ios", "5g", "titanium", "camera", "pro", "usb-c"]
        },
        new()
        {
            Id          = "phone-002",
            Sku         = "SAMSUNG-S25U-256",
            Name        = "Samsung Galaxy S25 Ultra 256 GB",
            Brand       = "Samsung",
            Category    = "Phones",
            Description = "Snapdragon 8 Elite, 12 GB RAM, 6.9\" QHD+ Dynamic AMOLED 2X 120 Hz, 200 MP quad camera, " +
                          "built-in S Pen, 5000 mAh battery, 45 W fast charging. Android 15 with Galaxy AI.",
            Price         = 1_299.99m,
            Currency      = "USD",
            StockQuantity = 14,
            Tags          = ["samsung", "android", "s-pen", "200mp", "5g", "amoled", "galaxy-ai"]
        },
        new()
        {
            Id          = "phone-003",
            Sku         = "GOOGLE-PIX9P-128",
            Name        = "Google Pixel 9 Pro 128 GB",
            Brand       = "Google",
            Category    = "Phones",
            Description = "Google Tensor G4 chip, 16 GB RAM, 6.3\" LTPO OLED 120 Hz, 50 MP camera with Magic Eraser and " +
                          "Best Take, 7 years OS + security updates, IP68, wireless charging.",
            Price         = 999.00m,
            Currency      = "USD",
            StockQuantity = 9,
            Tags          = ["google", "android", "tensor", "camera", "ai-photo", "5g", "pixel"]
        },
        new()
        {
            Id          = "phone-004",
            Sku         = "ONEPLUS-13-256",
            Name        = "OnePlus 13 256 GB",
            Brand       = "OnePlus",
            Category    = "Phones",
            Description = "Snapdragon 8 Elite, 12 GB RAM, 6.82\" LTPO AMOLED 120 Hz, 50 MP Hasselblad triple camera, " +
                          "6000 mAh battery, 100 W SUPERVOOC charging. OxygenOS 15.",
            Price         = 799.99m,
            Currency      = "USD",
            StockQuantity = 4,
            Tags          = ["oneplus", "hasselblad", "fast-charge", "android", "5g", "value"]
        },
        new()
        {
            Id          = "phone-005",
            Sku         = "SONY-XP1VII-256",
            Name        = "Sony Xperia 1 VII 256 GB",
            Brand       = "Sony",
            Category    = "Phones",
            Description = "Snapdragon 8 Elite, 12 GB RAM, 6.5\" 4K OLED 120 Hz, 52 MP Zeiss triple camera with real " +
                          "optical zoom (85–170 mm), 3.5 mm jack, IP68, Bravia display engine, 5000 mAh.",
            Price         = 1_099.99m,
            Currency      = "USD",
            StockQuantity = 2,
            Tags          = ["sony", "zeiss", "4k-display", "optical-zoom", "headphone-jack", "android", "5g"]
        },

        // ── Accessories ────────────────────────────────────────────────────────
        new()
        {
            Id          = "acc-001",
            Sku         = "SONY-WH1000XM6",
            Name        = "Sony WH-1000XM6 Wireless Headphones",
            Brand       = "Sony",
            Category    = "Accessories",
            Description = "Industry-leading noise cancellation with Dual Noise Sensor Technology, 30-hour battery, " +
                          "multipoint Bluetooth 5.3, LDAC, speak-to-chat, foldable design. Best-in-class ANC.",
            Price         = 349.99m,
            Currency      = "USD",
            StockQuantity = 25,
            Tags          = ["headphones", "anc", "wireless", "noise-cancelling", "ldac", "hi-res", "bluetooth"]
        },
        new()
        {
            Id          = "acc-002",
            Sku         = "APPLE-APP3",
            Name        = "Apple AirPods Pro (3rd Generation)",
            Brand       = "Apple",
            Category    = "Accessories",
            Description = "H3 chip, Active Noise Cancellation, Transparency mode, Adaptive Audio, personalized Spatial Audio, " +
                          "USB-C MagSafe case, 36-hour total battery life. IPX4 water resistant.",
            Price         = 279.00m,
            Currency      = "USD",
            StockQuantity = 30,
            Tags          = ["earbuds", "anc", "apple", "spatial-audio", "usb-c", "magsafe", "wireless"]
        },
        new()
        {
            Id          = "acc-003",
            Sku         = "LOGI-MXM4",
            Name        = "Logitech MX Master 4 Wireless Mouse",
            Brand       = "Logitech",
            Category    = "Accessories",
            Description = "MagSpeed electromagnetic scroll wheel, 8000 DPI Darkfield sensor works on any surface including glass, " +
                          "Logi Bolt + Bluetooth 5.1, 70-day battery, USB-C, Flow multi-device.",
            Price         = 109.99m,
            Currency      = "USD",
            StockQuantity = 18,
            Tags          = ["mouse", "wireless", "ergonomic", "scroll-wheel", "multi-device", "productivity"]
        },
        new()
        {
            Id          = "acc-004",
            Sku         = "SAMSUNG-65W-USBC",
            Name        = "Samsung 65 W USB-C Super Fast Charger",
            Brand       = "Samsung",
            Category    = "Accessories",
            Description = "65 W USB-C PD 3.0 GaN charger, compatible with Samsung Galaxy, iPhone 15/16, MacBook Air, " +
                          "iPad Pro, and most USB-C laptops. Compact fold-flat plug. 1.5 m cable included.",
            Price         = 49.99m,
            Currency      = "USD",
            StockQuantity = 50,
            Tags          = ["charger", "usb-c", "65w", "gan", "fast-charge", "laptop-compatible", "compact"]
        },
        new()
        {
            Id          = "acc-005",
            Sku         = "ANKER-MON27-4K",
            Name        = "Anker 27\" 4K USB-C Monitor",
            Brand       = "Anker",
            Category    = "Accessories",
            Description = "27\" IPS 4K UHD (3840×2160) panel, 99% sRGB, USB-C 90 W power delivery, HDMI 2.1, " +
                          "DisplayPort 1.4, built-in KVM switch, USB hub, adjustable stand. Flicker-free.",
            Price         = 599.99m,
            Currency      = "USD",
            StockQuantity = 7,
            Tags          = ["monitor", "4k", "usb-c", "ips", "kvm", "power-delivery", "27-inch"]
        }
    ];

    public IReadOnlyList<Product> GetAll() => _products;

    public IReadOnlyList<Product> FindBySkuOrName(string query)
    {
        return _products
            .Where(p =>
                p.Sku.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Brand.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

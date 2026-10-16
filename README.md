# 🛍️ Loma (AuraCommerce) — Online Store API

> The engine behind an online shop: products, shopping cart, orders, and a store design you can customize.

---

## 🧐 What is this, in plain words?

When you shop online you browse products, add them to a cart, and place an order. Behind the pretty pages there must be a "brain" that remembers the products, prices, your cart and your orders.

**Loma is that brain.** It lets a store:

- 🏷️ Show **products** organized in **categories**.
- 🛒 Let shoppers add items to a **cart** and place **orders**.
- 🔐 Let users **sign up / log in** securely.
- 🖼️ Upload and manage **images (media)** for products.
- 🎨 Change the store's **look & theme** from a built-in **customizer** — without touching code.

## 👥 Who uses it?

| Who | What they can do |
|---|---|
| **Shopper** | Browse, search categories, add to cart, order, track their orders |
| **Store owner** | Add/edit products and categories, upload images, manage orders, customize the store's design |

## 🔄 How it works (the simple story)

```
Shopper opens the store ➜ Browses products ➜ Adds to cart ➜ Logs in ➜ Places an order
Store owner ➜ Manages products & orders ➜ Customizes theme
```

---

## 🛠️ For developers

**Tech stack:** ASP.NET Core Web API (.NET 8) · JWT authentication · Entity Framework Core · In-memory caching · Brotli/Gzip response compression · Swagger · Angular front-end build served from `wwwroot/`

**API areas (controllers):** `Auth`, `Products`, `Categories`, `Cart`, `Orders`, `Media`, `Theme`, `Customizer`

> ⚠️ This repository currently contains the **API layer only**. The project references sibling projects `AuraCommerce.Application` and `AuraCommerce.Infrastructure`, which need to be added for the solution to build.

**Run locally**

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download).
2. Place the `AuraCommerce.Application` and `AuraCommerce.Infrastructure` projects next to this folder.
3. In `appsettings.json`, replace every `CHANGE_ME` (DB connection string, JWT secret, API keys).
4. Run:
   ```bash
   dotnet run
   ```
5. Open `/swagger` to try the API; the store front is served from the root URL.

---

## 🇪🇬 بالعربي

ده "العقل" اللي ورا متجر أونلاين: بيعرض المنتجات مقسّمة لأقسام، ويخلّي الزبون يحط حاجات في السلة ويعمل طلب، وبيخلّي صاحب المتجر يضيف منتجات وصور ويغيّر شكل المتجر وألوانه من لوحة تحكم، من غير ما يكتب كود.

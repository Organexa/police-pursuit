using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;

namespace PolicePursuit.Core
{
    // Google Play purchases through Unity IAP. A purchase is confirmed to Google only after it is saved,
    // so one interrupted half-way is delivered again at the next launch instead of being lost.
    public class Shop : MonoBehaviour
    {
        // Play Console -> Monetize -> Licensing. A public key; while empty the shop is hidden.
        static readonly string GooglePublicKey = "";

        public const string RemoveAds = "remove_ads";
        const string CarPrefix = "car_";

        // The same IDs must exist as in-app products in Play Console.
        public static readonly (string id, string name, int coins)[] Packs =
        {
            ("coins_small", "1000 coins", 1000),
            ("coins_medium", "5000 coins", 5000),
            ("coins_large", "15000 coins", 15000)
        };

        public static string CarProduct(Car car) => CarPrefix + car.Id;

        public static bool Configured => Application.platform == RuntimePlatform.Android && GooglePublicKey != "";

        public event Action Changed; // prices arrived or the save changed

        StoreController store;

        public async void Init()
        {
            if (!Configured) return;
            store = UnityIAPServices.StoreController();
            store.OnPurchasePending += OnPending;
            store.OnPurchaseConfirmed += _ => { };
            store.OnPurchaseFailed += order => Debug.LogWarning("Purchase failed: " + order.FailureReason);
            store.OnPurchasesFetched += orders =>
            {
                // Restores "Remove ads" and cars after a reinstall.
                foreach (var order in orders.ConfirmedOrders) Deliver(order);
            };
            store.OnPurchasesFetchFailed += e => Debug.LogWarning("Purchases not fetched: " + e.Message);
            store.OnProductsFetched += _ =>
            {
                Changed?.Invoke();
                store.FetchPurchases();
            };
            store.OnProductsFetchFailed += e => Debug.LogWarning("Products not fetched: " + e.FailureReason);
            store.OnStoreDisconnected += e => Debug.LogWarning("Store disconnected: " + e.Message);
            try
            {
                await store.Connect();
                var products = new List<ProductDefinition> { new ProductDefinition(RemoveAds, ProductType.NonConsumable) };
                products.AddRange(Packs.Select(p => new ProductDefinition(p.id, ProductType.Consumable)));
                products.AddRange(CarList.All.Where(c => c.Premium)
                    .Select(c => new ProductDefinition(CarProduct(c), ProductType.NonConsumable)));
                store.FetchProducts(products);
            }
            catch (Exception e) { Debug.LogWarning("Store not started: " + e.Message); }
        }

        // Google Play's price text, or null while the product cannot be bought.
        public string Price(string id)
        {
            var product = store?.GetProductById(id);
            return product != null && product.availableToPurchase ? product.metadata.localizedPriceString : null;
        }

        public void Buy(string id)
        {
            if (Price(id) != null) store.PurchaseProduct(id);
        }

        void OnPending(PendingOrder order)
        {
            if (Deliver(order)) store.ConfirmPurchase(order);
        }

        // False leaves the order unconfirmed: Google delivers it again, or refunds it after three days.
        bool Deliver(Order order)
        {
            var save = SaveData.Load(SaveData.DefaultPath);
            bool handled = false;
            foreach (var item in order.CartOrdered.Items())
            {
                var id = item.Product.definition.id;
                if (!Genuine(order.Info.Receipt, id) || !Grant(save, id, order.Info.TransactionID)) return false;
                handled = true;
            }
            if (!handled || !save.Save(SaveData.DefaultPath)) return false;
            Changed?.Invoke();
            return true;
        }

        // shortcut: checked on the phone only (PROGRESS risk 4); add server verification if cheating matters.
        static bool Genuine(string receipt, string id)
        {
            try
            {
                var validator = new CrossPlatformValidator(Convert.FromBase64String(GooglePublicKey), Application.identifier);
                return validator.Validate(receipt).Any(r => r.productID == id);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Receipt refused: " + e.Message);
                return false;
            }
        }

        // Adds the product to the save. Safe to repeat: coins are added once per transaction.
        public static bool Grant(SaveData save, string id, string transactionId)
        {
            if (id == RemoveAds)
            {
                save.adsRemoved = true;
                return true;
            }
            var car = CarList.All.FirstOrDefault(c => c.Premium && CarProduct(c) == id);
            if (car != null)
            {
                if (!save.Owns(car)) save.unlockedCars.Add(car.Id);
                return true;
            }
            var pack = Packs.FirstOrDefault(p => p.id == id);
            if (pack.id == null || string.IsNullOrEmpty(transactionId)) return false;
            if (save.delivered.Contains(transactionId)) return true;
            save.coins += pack.coins;
            save.delivered.Add(transactionId);
            if (save.delivered.Count > 100) save.delivered.RemoveAt(0);
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using LibraryData;
using LibraryData.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace LibraryServices
{
    public class CheckoutService : ICheckout
    {
        private readonly LibraryContext _context;
        public CheckoutService(LibraryContext context)
        {
            _context = context;
        }

        public void Add(Checkout newCheckout)
        {
            _context.Add(newCheckout);
            _context.SaveChanges();
        }

        public IEnumerable<Checkout> GetAll()
        {
           return _context.Checkouts;
        }

        public Checkout GetById(int checkoutId)
        {
            return _context.Checkouts.FirstOrDefault(checkout => checkoutId == checkout.Id);
        }

        public IEnumerable<CheckoutHistory> GetCheckoutHistory(int id)
        {
            return _context.CheckoutHistories.Include(h => h.LibraryAsset).Include(h => h.LibraryCard)
                .Where(h => h.LibraryAsset.Id == id); 
        }

        public IEnumerable<Hold> GetCurrentHolds(int id)
        {
            return _context.Holds
                .Include(hold => hold.LibraryAsset)
                .Where(h => h.LibraryAsset.Id == id);
        }

        public Checkout GetLatestCheckout(int assetId)
        {
            return _context.Checkouts.Where(c => c.LibraryAsset.Id == assetId)
                .OrderByDescending(c => c.Since)
                .FirstOrDefault();
        }
        public void MarkFound(int assetId)
        {
            var item = _context.LibraryAssets.FirstOrDefault(a => a.Id == assetId);

            _context.Update(item);

            item.Status = _context.Statuses.FirstOrDefault(s => s.Name == "Available");
            //remove any existing checkouts

            var checkout = _context.Checkouts.FirstOrDefault(c => c.LibraryAsset.Id == assetId);
            if (checkout != null)
            {
                _context.Remove(checkout);
            }
            //close any existing checkout history
            var history = _context.CheckoutHistories.FirstOrDefault(h => h.LibraryAsset.Id == assetId && h.CheckedIn == null);
            if (history != null)
            {
                _context.Update(history);
                history.CheckedIn = DateTime.Now;
            }
            _context.SaveChanges();
        }

        public void MarkLost(int assetId)
        {
            var item = _context.LibraryAssets.FirstOrDefault(asset => asset.Id == assetId);

            _context.Update(item);

            item.Status = _context.Statuses.FirstOrDefault(status => status.Name == "Lost");

            _context.SaveChanges();
        }

        public void PlaceHold(int assetId, int LibraryCardId)
        {
            var now = DateTime.Now;

            var asset = _context.LibraryAssets.Include(a => a.Status).FirstOrDefault(a => a.Id == assetId);
            var libraryCard = _context.LibraryCards.FirstOrDefault(l => l.Id == LibraryCardId);

            if (asset.Status.Name == "Available")
            {
                _context.Update(asset);
                asset.Status = _context.Statuses.FirstOrDefault(s => s.Name == "On Hold");
            }

            var hold = new Hold()
            {
                HoldPlaced = now,
                LibraryAsset = asset,
                LibraryCard = libraryCard
            };

            _context.Add(hold);
            _context.SaveChanges();
        }
        public void CheckInItem(int assetId)
        {
            var now = DateTime.Now;
            var item = _context.LibraryAssets.FirstOrDefault(a => a.Id == assetId);
            _context.Update(item);
            //remove the checkout
            var Checkout = _context.Checkouts.Where(c => c.LibraryAsset.Id == assetId);
            if (Checkout != null)
            {
                _context.Remove(Checkout);
            }
            //Close any existing checkout history
            var history = _context.CheckoutHistories.FirstOrDefault(h => h.LibraryAsset.Id == assetId && h.CheckedIn == null);
            if (history != null)
            {
                _context.Update(history);
                history.CheckedIn = now;
            }

            var currentHolds = _context.Holds.Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .Where(h => h.LibraryAsset.Id == assetId);
            if (currentHolds.Any())
            {
                var earliestHold = currentHolds.OrderBy(h => h.HoldPlaced).FirstOrDefault(); //Default is Ascending
                _context.Remove(earliestHold);
                _context.Update(earliestHold.LibraryAsset);
                earliestHold.LibraryAsset.Status = _context.Statuses.FirstOrDefault(s => s.Name == "Checked Out");
                var newCheckout = new Checkout()
                {
                    LibraryCard = earliestHold.LibraryCard,
                    LibraryAsset = earliestHold.LibraryAsset,
                    Since = earliestHold.HoldPlaced
                };
                _context.Checkouts.Add(newCheckout);
                
                var newCheckoutHistrory = new CheckoutHistory()
                {
                    LibraryAsset = earliestHold.LibraryAsset,
                    LibraryCard = earliestHold.LibraryCard,
                    CheckOut = now
                };

                _context.CheckoutHistories.Add(newCheckoutHistrory);
                _context.SaveChanges();
                return;
            }

            item.Status = _context.Statuses.FirstOrDefault(s => s.Name == "Available");

            _context.SaveChanges();
        }

        public void CheckOutItem(int assetId, int LibraryCardId)
        {
            var now = DateTime.Now;
            var LibraryCard = _context.LibraryCards.FirstOrDefault(l => l.Id == LibraryCardId);
            var LibraryAsset = _context.LibraryAssets.FirstOrDefault(a => a.Id == assetId);
            
            _context.Update(LibraryAsset);
            LibraryAsset.Status = _context.Statuses.FirstOrDefault(s => s.Name == "Checked Out");
            
            var newCheckout = new Checkout()
            {
                LibraryCard = LibraryCard,
                LibraryAsset = LibraryAsset,
                Since = now
            };
            _context.Checkouts.Add(newCheckout);

            var newCheckoutHistrory = new CheckoutHistory()
            {
                LibraryAsset = LibraryAsset,
                LibraryCard = LibraryCard,
                CheckOut = now
            };

            _context.CheckoutHistories.Add(newCheckoutHistrory);

            _context.SaveChanges();
        }
        public string GetCurrentHoldPatronName(int holdId)
        {
            var hold = _context.Holds.Include(h => h.LibraryCard).Include(h => h.LibraryAsset)
                .FirstOrDefault(h => h.Id == holdId);

            var cardId = hold?.LibraryCard.Id;
            var patron = _context.Patrons.Include(p => p.LibraryCard).FirstOrDefault(p => p.LibraryCard.Id == cardId);

            return patron?.FirstName + " " + patron?.LastName;
        }

        public DateTime GetCurrentHoldPlaced(int holdId)
        {
            return _context.Holds.Include(h => h.LibraryCard).Include(h => h.LibraryAsset)
                .FirstOrDefault(h => h.Id == holdId).HoldPlaced;
        }

        public string GetCurrentMethodCheckoutPatron(int assetId)
        {
            var checkout = _context.Checkouts.Include(co => co.LibraryAsset).Include(co => co.LibraryCard)
                .Where(co => co.LibraryAsset.Id == assetId)
                .FirstOrDefault();

            if (checkout == null)
            {
                return "Not checked out";
            }

            var cardId = checkout.LibraryCard.Id;
            var patron = _context.Patrons
                .Include(p => p.LibraryCard)
                .FirstOrDefault(p => p.LibraryCard.Id == cardId);

            return patron.FirstName + " " + patron.LastName;
        }
    }
}

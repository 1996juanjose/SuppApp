export interface ProductPriceItem {
  id: number;
  quantity: number;
  price: number;
}

export interface ProductItem {
  id: number;
  companyId?: number | null;
  name: string;
  purchaseUnitCost: number;
  isActive: boolean;
  prices: ProductPriceItem[];
}

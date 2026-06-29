export interface RecordAlert {
  id: number;
  companyId?: number | null;
  cellphone: string;
  nameOrReference: string;
  callActivity: string;
  callScheduledAt: string | null;
  isDue: boolean;
}

export interface RecordItem {
  id: number;
  statusCatalogId: number;
  statusName?: string;
  recordDate: string;
  createdAt: string;
  cellphone: string;
  nameOrReference: string;
  callActivity: string;
  dni: string;
  companyId?: number | null;
  productCatalogId?: number | null;
  quantity: number;
  productAmount: number;
  paidAmount: number;
  activePaidAmount?: number;
  balanceDue: number;
  calculatedBalanceDue?: number;
  folderPath: string;
  destino: string;
  clave: string;
  guia: string;
  createdByUserId: string;
  createdByUserName: string;
  callScheduledAt?: string | null;
  isCallConcrete?: boolean;
  badgeClass?: string;
}

export interface RecordSummaryResponse {
  now: string;
  upcoming: RecordAlert[];
  due: RecordAlert[];
}

export interface RecordAlertResponse {
  now: string;
  nextCallScheduledAt: string | null;
  alerts: RecordAlert[];
}

export interface RecordStatusOption {
  id: number;
  name: string;
  sortOrder: number;
  badgeClass?: string;
}

export interface RecordProductPriceOption {
  id: number;
  quantity: number;
  price: number;
}

export interface RecordProductOption {
  id: number;
  name: string;
  purchaseUnitCost: number;
  prices: RecordProductPriceOption[];
}

export interface RecordPaymentItem {
  id: number;
  amount: number;
  paymentDate: string;
  createdAt: string;
  proofImagePath: string;
  proofFileName: string;
  operationNumber: string;
  createdByUserId: string;
  createdByUserName: string;
  isReversed: boolean;
  reversedAt?: string | null;
  reversedByUserId: string;
  reversedByUserName: string;
}

export interface RecordDetail extends RecordItem {
  productName?: string | null;
  payments: RecordPaymentItem[];
  badgeClass?: string;
}

export interface RecordUpdateRequest {
  statusCatalogId: number;
  recordDate: string;
  cellphone: string;
  nameOrReference?: string | null;
  callActivity?: string | null;
  callScheduledAt?: string | null;
  isCallConcrete: boolean;
  dni?: string | null;
  productId?: number | null;
  quantity: number;
  folderPath?: string | null;
  destino?: string | null;
  clave?: string | null;
  guia?: string | null;
}

export interface RecordFilter {
  search?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  companyId?: number | null;
  statusIds?: number[];
}


export interface RecordProductsOption {
  id: number;

  name: string;

  purchaseUnitCost: number;

  prices: RecordProductPriceOption[];

  commissionTiers: RecordProductCommissionTierOption[];

  stockMovements: RecordProductStockMovementOption[];
}

export interface RecordProductPriceOption {
  id: number;

  quantity: number;

  price: number;
}

export interface RecordProductCommissionTierOption {
  id: number;

  quantity: number;

  commissionRate: number;
}

export interface RecordProductStockMovementOption {
  id: number;

  quantity: number;

  unitCost: number;

  movementType: string;

  movementDate: string;

  totalCost: number;
}

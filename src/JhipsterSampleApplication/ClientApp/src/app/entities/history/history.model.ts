export interface IHistory {
  id?: number;
  user?: string | null;
  domain?: string | null;
  text?: string | null;
}

export type NewHistory = Omit<IHistory, 'id'> & { id: null };

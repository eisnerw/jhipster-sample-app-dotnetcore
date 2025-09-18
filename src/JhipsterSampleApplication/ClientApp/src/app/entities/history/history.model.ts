export interface IHistory {
  id?: number;
  user?: string | null;
  entity?: string | null;
  text?: string | null;
}

export type NewHistory = Omit<IHistory, 'id'> & { id: null };

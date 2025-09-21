export interface INamedQuery {
  id: number;
  name: string;
  text: string;
  owner: string;
  entity: string;
}

export type NewNamedQuery = Omit<INamedQuery, 'id'> & { id: null };

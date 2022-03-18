export interface IStoredRuleset {
  id?: number;
  name?: string;
  jsonString?: string;
  bDelete?: boolean;
}

export class StoredRuleset implements IStoredRuleset {
  public bDelete? : boolean;
  constructor(public id?: number, public name?: string, public jsonString?: string) {}
}

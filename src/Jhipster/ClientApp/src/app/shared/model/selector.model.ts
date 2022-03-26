export interface ISelector {
  id?: number;
  name?: string;
  rulesetName?: string;
  action?: string;
  actionParameter?: string;
  description?: string;
}

export class 
Selector implements ISelector {
  constructor(
    public id?: number,
    public name?: string,
    public rulesetName?: string,
    public action?: string,
    public actionParameter?: string,
    public description?: string
  ) {}
}

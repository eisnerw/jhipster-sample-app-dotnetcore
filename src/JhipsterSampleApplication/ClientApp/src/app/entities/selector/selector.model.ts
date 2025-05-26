export interface ISelector {
  id?: number;
  name?: string | null;
  rulesetName?: string | null;
  action?: string | null;
  actionParameter?: string | null;
  description?: string | null;
}

export class Selector implements ISelector {
  constructor(
    public id?: number,
    public name?: string | null,
    public rulesetName?: string | null,
    public action?: string | null,
    public actionParameter?: string | null,
    public description?: string | null,
  ) {}
}

export function getSelectorIdentifier(selector: ISelector): number | undefined {
  return selector.id;
}

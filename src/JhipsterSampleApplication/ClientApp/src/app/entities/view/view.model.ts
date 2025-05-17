export interface IView {
  id?: string;
  name: string;
  field: string;
  aggregation?: string;
  query?: string;
  categoryQuery?: string;
  script?: string;
  primaryViewId?: string;
  primaryView?: IView;
}

export class View implements IView {
  constructor(
    public id?: string,
    public name = '',
    public field = '',
    public aggregation?: string,
    public query?: string,
    public categoryQuery?: string,
    public script?: string,
    public primaryViewId?: string,
    public primaryView?: IView,
  ) {}
}

export interface ISupreme {
	id?: string;
	name?: string;
	term?: string;
	docket_number?: string;
	heard_by?: string;
	justia_url?: string;
}

export class Supreme implements ISupreme {
	constructor(
		public id?: string,
		public name?: string,
		public term?: string,
		public docket_number?: string,
		public heard_by?: string,
		public justia_url?: string,
	) {}
}

export interface IViewResult {
	categoryName: string;
	count: number;
}
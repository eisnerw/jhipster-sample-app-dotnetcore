export interface ISupreme {
	id?: string;
	name?: string;
        term?: number;
	docket_number?: string;
	justia_url?: string;
	decision?: string;
	description?: string;
	dissent?: string;
	lower_court?: string;
	manner_of_jurisdiction?: string;
	opinion?: string;
	argument2_url?: string;
	appellant?: string;
	appellee?: string;
	petitioner?: string;
	respondent?: string;
	recused?: string[];
	majority?: string[];
	minority?: string[];
	advocates?: string[];
	facts_of_the_case?: string;
	question?: string;
	conclusion?: string;
}

export class Supreme implements ISupreme {
	constructor(
		public id?: string,
		public name?: string,
                public term?: number,
		public docket_number?: string,
        public justia_url?: string,
        public decision?: string,
        public description?: string,
        public dissent?: string,
        public lower_court?: string,
        public manner_of_jurisdiction?: string,
        public opinion?: string,
        public argument2_url?: string,
        public appellant?: string,
        public appellee?: string,
        public petitioner?: string,
        public respondent?: string,
        public recused?: string[],
        public majority?: string[],
        public minority?: string[],
        public advocates?: string[],
        public facts_of_the_case?: string,
        public question?: string,
        public conclusion?: string,
	) {}
}

export interface IViewResult {
	categoryName: string;
	count: number;
}
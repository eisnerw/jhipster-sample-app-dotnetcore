import { Option } from "angular2-query-builder";

export interface IQueryRule {
  field: string,
  operator: string,
  value: string | string[]
}

export interface IQuery {
  condition: string,
  rules: IQueryRule[],
  not: boolean,
  name?: string,
  Invalid?: boolean,
  position?: number
}

interface IParse {
  matches: boolean,
  string: string,
  i: number
}

export class BirthdayQueryParserService {
  queryNames: string[] = [];
  parse(query: string, rulesetMap: Map<string, IQuery | IQueryRule>, optionsMap : Map<string, Option[]>): IQuery {
    if (query.trim() === ""){
      return {"condition":"or","not":false,"rules":[]};
    }
    this.queryNames = [...(rulesetMap as Map<string, IQuery>).keys()].sort((a, b) => a > b ? -1 : 1);;
    const queryNameRegexString = this.queryNames.length > 0 ? "|(" + this.queryNames.join("|") + ")": "";
    const regexString = "\\s*(" + '(?<=IN\\s)(\\("(\\\\"|[^"])+"|\\/(\\\\/|[^/]+\\/)|[^"\\s]+\\s*)(,\\s*("(\\\\"|[^"])+"|[^"\\s]+\\s*))*\\s*\\)' + queryNameRegexString + '|[()]' + '|("(\\\\"|\\\\\\\\|[^"])+\\"|\\/(\\\\\\/|[^\\/])+\\/i?' + "|sign|dob|lname|fname|isAlive|document)|(=|!=|CONTAINS|LIKE|EXISTS|!EXISTS|IN|!IN|>=|<=|>|<)|(&|\\||!)|[^\"/=!<>() ]+)\\s*";
    const regex = new RegExp(regexString, "g");
    const tokens = query.replace(regex, '`$1').split('`');
    /* NOT SURE WE STILL NEED THIS
    // join adjacent words
    let looping = tokens.length > 2;
    while (looping){
      for (let iTokens = 1; iTokens < (tokens.length - 1); iTokens++){
        looping = false;
        if (!/^(&|\||CONTAINS|LIKE|CO|CON|CONT|CONTA|CONTAI|CONTAIN|LI|LIK)$/.test(tokens[iTokens])
          && /^[\w\d".*-]+/.test(tokens[iTokens])
          && !/^(&|\||CONTAINS|LIKE|CO|CON|CONT|CONTA|CONTAI|CONTAIN|LI|LIK)$/.test(tokens[iTokens + 1])
          && /^[\w\d".*-]+/.test(tokens[iTokens + 1])){
            tokens[iTokens] += (" " + tokens[iTokens + 1]);
            tokens.splice(iTokens + 1, 1);
            looping = tokens.length > 2;
            break;
          }

      }
    } */
    if (tokens[0] !== ""){
      return { Invalid :true, position: 0, condition: "", rules:[], not: false} // must be starting with unmatched " or /
    }
    const i = 1;
    let ret = this.parseRuleset(tokens, i, false, rulesetMap, optionsMap);
    if (!ret.matches){
      ret = this.parseRule(tokens, i, optionsMap);
    }
    if (!ret.string.startsWith('{"condition')){
      ret.string = '{"condition":"or","rules":[' + ret.string + '],"not":false}';
    }
    if (!ret.matches || ret.i < tokens.length){
      return { Invalid :true, position: ret.i, condition: "", rules:[], not: false}
    }
    return this.normalize(JSON.parse(ret.string), rulesetMap as Map<string, IQuery>);
  }

  normalize(query: IQuery, rulesetMap: Map<string, IQuery>): IQuery{
    if (query.name && rulesetMap && rulesetMap.has(query.name)){
      return rulesetMap?.get(query.name) as IQuery;
    }
    if (query.name){
      rulesetMap.set(query.name, query);
    }
    for (let i = 0; i < query.rules.length; i++){
      const testQuery = query.rules[i] as any as IQuery;
      if (testQuery.rules){
        (query.rules[i] as any) = this.normalize(testQuery, rulesetMap);
      }
    }
    return query;
  }

  parseRule(tokens: string[], i: number, optionsMap : Map<string, Option[]>):IParse{
    const parse: IParse = {
      matches: false,
      string: "",
      i
    }
    if (i >= tokens.length){
      return parse;
    }
    if (!/^(sign|dob|lname|fname|isAlive|document)$/.test(tokens[parse.i])){
        if ((/^[A-Za-z0-9?*]+$/.test(tokens[i]) && /[a-z0-9]/.test(tokens[i])) || /^".*"$/.test(tokens[i]) || /^\/.*\/i?$/.test(tokens[i])){
            let documentValue = '"' + tokens[i].replace(/([\\"])/g, '\\$1') + '"';
            if (tokens[i].startsWith('"')){
              if (tokens[i].endsWith('\\"')){
                parse.string = '[invalid quoted string]';
                return parse;
              }
              documentValue = tokens[i];
            }
            if (typeof documentValue === 'string' && documentValue.startsWith('"/') && (documentValue.endsWith('/"') || documentValue.endsWith('/i"'))){
              try {
                RegExp(documentValue.substring(1, documentValue.length - 1));
              } catch(e){
                parse.string = '[bad regex]'
                return parse;
              }
            }
            parse.matches = true;
            parse.string = '{"field":"document", "operator":"contains","value":' + documentValue + '}';
            parse.i++;
            return parse;
        }
        parse.string = '[invalid field name]';
        return parse;
    }
    if ((i + 2) > tokens.length){
        return parse;
    }
    parse.i++;
    parse.string = '[invalid operator]'
    switch (tokens[i]){
      case 'isAlive':
        if (tokens[i + 1] !== '='){
          return parse;
        }
        break;

      case 'sign':
        if (!/^(=|!=|IN|!IN)$/.test(tokens[i + 1])){
          return parse;
        }
        break;

      case 'dob':
        if (!/^(=|!=|>=|<=|>|<|EXISTS|!EXISTS)$/.test(tokens[i + 1])){
          return parse;
        }
        break;

      case 'lname':
        if (!/^(=|!=|IN|!IN|EXISTS|!EXISTS)$/.test(tokens[i + 1])){
          return parse;
        }
        break;

      case 'fname':
        if (!/^(=|!=|CONTAINS|LIKE|IN|!IN|EXISTS|!EXISTS)$/.test(tokens[i + 1])){
          return parse;
        }
        break;

      case 'document':
        if (tokens[i + 1] !== 'CONTAINS'){
          return parse;
        }
        break;

      default:
        return parse;
        break;
    }
    parse.i++;
    parse.string = '[invalid value]';
    if (tokens[i + 1] === "EXISTS" || tokens[i + 1] === "!EXISTS"){
      parse.matches = true;
      parse.i++;
      parse.string = '{"field":"' + tokens[i] + '","operator":"exists","value":' + (tokens[i + 1].startsWith('!') ? 'false' : 'true') + '}';
      return parse;
    }
    const values : Map<string, string> = new Map<string, string>();
    switch (tokens[i]){
      case 'isAlive':
        if (!/^(true|false)$/.test(tokens[i + 2])){
          return parse;
        }
        break;

      case 'sign':
        if ((tokens[i + 1] === 'IN' || tokens[i + 1] === '!IN') && tokens[i + 2] && tokens[i + 2].length > 2){
          let bValid = true;
          const trimmedAndQuoted : string[] = [];
          tokens[i + 2].substring(1, tokens[i + 2].length - 1).split(',').forEach(v=>{
            let value = v.trim().toLowerCase();
            if (value.startsWith('"') && value.endsWith('"') && value.length > 2){
              value = value.substring(1, value.length - 1);
            }
            if (!/^(aries|taurus|gemini|cancer|leo|virgo|libra|scorpio|sagittarius|capricorn|aquarius|pisces)$/.test(value)){
              bValid = false;
            } else {
              trimmedAndQuoted.push('"' + value + '"');
            }
          });
          if (!bValid){
            return parse;
          }
          tokens[i + 2] = "[" + trimmedAndQuoted.join(", ") + "]";
        } else {
          if (tokens[i + 2] && tokens[i + 2].startsWith('"') && tokens[i + 2].length > 2){
            tokens[i + 2] = tokens[i + 2].substring(1, tokens[i + 2].length - 1);
          }
          if (!/^(aries|taurus|gemini|cancer|leo|virgo|libra|scorpio|sagittarius|capricorn|aquarius|pisces|EXISTS)$/.test(tokens[i + 2])){
            return parse;
          }
        }
        break;

      case 'dob':
        if (!/^\d{4,4}-\d{2,2}-\d{2,2}$/.test(tokens[i + 2])){
          return parse;
        }
        break;

      case 'lname':
        if (optionsMap.has('lname')){
          optionsMap.get('lname')?.forEach(o=>{
            if (!values.has(o.value.toLowerCase())){
              values.set(o.value.toLowerCase(), o.value);
            }
          });
        }
        if ((tokens[i + 1] === 'IN' || tokens[i + 1] === '!IN') && tokens[i + 2] && tokens[i + 2].length > 2){
          let bValid = true;
          const trimmedAndQuoted : string[] = [];
          tokens[i + 2].substring(1, tokens[i + 2].length - 1).split(',').forEach(v=>{
            let value = v.trim().toLowerCase();
            if (value.startsWith('"')){
              value = value.substring(1, value.length - 1).replace(/\\"/g,'"');
            }
            const quotedValue = '"' + value + '"';
            if (!values.has(value)){
              bValid = false;
            } else {
              trimmedAndQuoted.push(quotedValue);
            }
          });
          if (!bValid){
            return parse;
          }
          tokens[i + 2] = "[" + trimmedAndQuoted.join(", ") + "]";
        } else {
          let value = tokens[i + 2]?.toLowerCase();
          if (value?.startsWith('"')){
            value = value.substring(1, value.length - 1).replace(/\\"/g,'"');
          }
          const quotedValue = '"' + value + '"';
          if (!values.has(value)){
            return parse;
          }
          tokens[i + 2] = quotedValue;
        }
        break;

      case 'fname':
        if (tokens[i + 1] === 'CONTAINS' && typeof tokens[i + 2] === 'string' && tokens[i + 2].startsWith('/') && (tokens[i + 2].endsWith('/') || tokens[i + 2].endsWith('/i'))){
          try {
            RegExp(tokens[i + 2]);
          } catch(e){
            parse.string = '[bad regex]'
            return parse;
          }
        } else if (!((tokens[i + 1] === 'IN' || tokens[i + 1] === '!IN') && /^\(.+\)$/.test(tokens[i + 2]))
            && !/^[\w\d.* -]+$/.test(tokens[i + 2]) && !/^"[^"]+"$/.test(tokens[i + 2])){
          return parse;
        }
        break;

      case 'document':
        if (!/^[\w\d.* -]+$/.test(tokens[i + 2]) && !/^"[^"]+"$/.test(tokens[i + 2])){
          return parse;
        }
        break;

      default:
        return parse;
    }
    parse.i++;
    let value = "";
    if (tokens[i + 2] === undefined){
      return parse; // no value
    }
    if (tokens[i].startsWith("is") || tokens[i + 1] === "IN" || tokens[i + 1] === "!IN" || value || tokens[i + 2].startsWith('"')){
      value = tokens[i + 2];
    } else {
      value = '"' + tokens[i + 2] + '"';
    }
    parse.matches = true;
    const  op = tokens[i + 1] === "!IN" ? "not in" : tokens[i + 1].toLowerCase();
    parse.string = '{"field":"' + tokens[i] + '","operator":"' + op + '","value":' + value + '}';
    return parse;
  }

  parseRuleset(tokens: string[], i: number, not: boolean, rulesetMap : Map<string, IQuery | IQueryRule>, optionsMap : Map<string, Option[]>):IParse{
    let ret = this.parseAndOrRuleset(tokens, i, not, rulesetMap, optionsMap);
    if (!ret.matches){
      if (ret.string !== ""){
        return ret;
      }
      ret = this.parseNotRuleset(tokens, i, rulesetMap, optionsMap);
    }
    if (!ret.matches){
      ret = this.parseParened(tokens, i, rulesetMap, optionsMap);
    }
    return ret;
  }

  parseAndOrRuleset(tokens: string[], i: number, not: boolean, rulesetMap : Map<string, IQuery | IQueryRule>, optionsMap : Map<string, Option[]>):IParse{
    const rules : string[] = [];
    const parse: IParse = {
      matches: false,
      string: "",
      i
    }
    let ret = this.parseParened(tokens, i, rulesetMap, optionsMap);
    if (!ret.matches){
      if (ret.string !== ""){
        return ret;
      }
      ret = this.parseRule(tokens, i, optionsMap);
      if (!ret.matches){
        if (ret.string !== ""){
          return ret;
        }
        return parse;
      }
    }
    if (!/^(&|\|)$/.test(tokens[ret.i])){
      if (not && tokens[ret.i] === ")"){ // strange condition caused by !(named_query)
        return {
          matches: true,
          i: ret.i,
          string: '{"condition":"or","rules":[' + ret.string + '],"not": true}'
        }
      }
      if (ret.matches){
        return ret;
      }
      return parse;
    }
    const condition = tokens[ret.i];
    parse.i = ret.i + 1;
    parse.matches = true;
    rules.push(ret.string);
    let loop = true;
    while (loop){
      ret = this.parseParened(tokens, parse.i, rulesetMap, optionsMap);
      if (!ret.matches){
        if (ret.string !== ""){
          return ret;
        }
        ret = this.parseRule(tokens, parse.i, optionsMap);
        if (!ret.matches){
          if (ret.string !== ""){
            return ret;
          }
          loop = false;
        }
      }
      if (ret.matches){
        rules.push(ret.string);
        parse.i = ret.i;
        if (tokens[ret.i] !== condition){
          loop = false;
        } else {
          parse.i++;
        }
      } else {
        loop = false;
      }
    }
    if (rules.length < 2){
        parse.matches = false;
        return parse;
    }
    parse.string = '{"condition":"' + (condition === "&" ? "and" : "or") + '","rules":[' + rules.join(',') + '],"not":' + (not ? 'true' : 'false') + '}'
    return parse;
  }

  parseNotRuleset(tokens: string[], i: number, rulesetMap : Map<string, IQuery | IQueryRule>, optionsMap : Map<string, Option[]>):IParse{
    const parse: IParse = {
      matches: false,
      string: "",
      i
    }
    if (tokens[i++] !== "!"){
      return parse;
    }
    const ret = this.parseParened(tokens, i, rulesetMap, optionsMap);
    if (!ret.matches){
      if (ret.string !== ""){
        return ret;
      }
      parse.string = "[! is not followed by parenthesized expression]";
      return parse;
    } else {
      const obj = JSON.parse(ret.string);
      obj.not = true;
      ret.string = JSON.stringify(obj);
    }
    return ret;
  }

  parseParened(tokens: string[], i: number, rulesetMap : Map<string, IQuery | IQueryRule>, optionsMap : Map<string, Option[]>):IParse{
    const parse: IParse = {
      matches: false,
      string: "",
      i
    }
    let not = false;
    if (tokens[i] === "!"){
      i++;
      not = true;
    }
    if (this.queryNames.includes(tokens[i])){
      if (not){
        // the named query must be nested in a parenthis to add NOT
        return {
          matches: true,
          i: i + 1,
          string: '{"condition":"or","rules":[' + JSON.stringify(rulesetMap?.get(tokens[i])) + '],"not": true}'
        }
      }
      return {
        matches: true,
        string: JSON.stringify(rulesetMap?.get(tokens[i])) ,
        i: i + 1
      };
    }
    if (tokens[i++] !== "("){
      return parse;
    }
    parse.i++;
    let ret = this.parseRuleset(tokens, i, not, rulesetMap, optionsMap);
    if (!ret.matches && ret.string === ""){
      ret = this.parseRule(tokens, i, optionsMap);
      if (ret.matches){
        ret.string = ret.string = '{"condition":"or","rules":[' + ret.string + '],"not":' + (not ? 'true' : 'false') + '}';
      } else {
        return ret;
      }
    }
    if (tokens[ret.i] !== ')'){
      ret.matches = false;
      ret.string = "[missing right paren]";
    } else {
      ret.i++;
    }
    return ret;
  }

  queryAsString(query : IQuery, recurse?: boolean): string{
    let result = "";
    let multipleConditions = false;
    query.rules.forEach((r)=>{
      if (result.length > 0){
        result += (' ' + (query.condition === "and" ? "&" : "|") + ' ');
        multipleConditions = true;
      }
      if ((r as any).condition !== undefined){
        const ruleQuery: IQuery = r as any as IQuery;
        if (ruleQuery.name){
          result += ruleQuery.name;
          // rulesetMap?.set(ruleQuery.name, ruleQuery);
        } else {
          result += this.queryAsString(r as unknown as IQuery, query.rules.length > 1); // note: is only one rule, treat it as a top level
        }
      } else if (r.field === "document" && r.value !== undefined) {
        if(/^\/.*\//.test(r.value.toString())){
          // regex
          result += r.value.toString();
        } else if (!/^[a-zA-Z\d]+$/.test(r.value.toString())){
          result += ('"' + r.value.toString().replace(/([\\"])/g, '\\$1') + '"');
        } else {
          result += (r.value.toString().toLowerCase());
        }
      } else {
        result += r.field;
        if (r.operator === "exists"){
          result += (' ' + (r.value ? '' : '!') + 'EXISTS ');
        } else if (r.operator === "in" || r.operator === "not in"){
          const quoted : string[] = [];
          (r.value as string[]).forEach(v =>{
            quoted.push(/^[a-zA-Z\d]+$/.test(v) ? v : ('"' + v?.replace(/([\\"])/g, '\\$1') + '"'));
          });
          result += (' ' + (r.operator === "in" ? "" : "!") +'IN (' + quoted.join(', ') + ') ');
        } else {
          result += (' ' +  r.operator.toUpperCase() + ' ');
          if (r.value !== undefined) {
            if (r.value.toString().startsWith('/') && (r.value.toString().endsWith('/') || r.value.toString().endsWith('/i'))){
              result += r.value.toString(); // regex
            } else if (/[\s\\"]/.test(r.value.toString())){
              result += ('"' + r.value.toString().replace(/([\\"])/g, '\\$1') + '"');
            } else {
              result += (r.value.toString().toLowerCase());
            }
          }
        }
      }
    });
    if (query.not){
      if (query.rules.length === 1 && (query.rules[0] as any as IQuery).name){
        result = '!' + result;
      } else {
        result = '!(' + result + ')';
      }
    } else if (recurse && multipleConditions){
      result = '(' + result + ')';
    }
    return result;
  }
  simplifyQuery(query: IQuery):void{
    query.rules.forEach(r=>{
      if ((r as any).rules !== undefined){
        // rule is a query
        this.simplifyQuery(r as unknown as IQuery);
      }
    });
    if (query.rules.length === 1 &&
        (query.rules[0] as any).rules !== undefined &&
        (query.rules[0] as any).rules.length === 1 &&
        (query as any).name !== undefined){
      // remove one level
      query.rules = [(query.rules[0] as any).rules[0]];
    }
  }
}

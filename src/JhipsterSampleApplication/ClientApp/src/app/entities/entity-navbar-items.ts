import NavbarItem from 'app/layouts/navbar/navbar-item.model';

export const ENTITY_NAV_ITEMS: NavbarItem[] = [
  {
    name: 'Movie',
    route: '/movie',
    translationKey: 'global.menu.entities.movie',
  },
  {
    name: 'Birthday',
    route: '/birthday',
    translationKey: 'global.menu.entities.birthday',
  },
  {
    name: 'Supreme',
    route: '/supreme',
    translationKey: 'global.menu.entities.supreme',
  },
  {
    name: 'Entity',
    route: '/entity',
    translationKey: 'global.menu.entities.entity',
  },
  {
    name: 'NamedQuery',
    route: '/named-query',
    translationKey: 'global.menu.entities.namedQuery',
  },
  {
    name: 'Selector',
    route: '/selector',
    translationKey: 'global.menu.entities.selector',
  },
  {
    name: 'History',
    route: '/history',
    translationKey: 'global.menu.entities.history',
  },
];

using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JHipsterNet.Core.Pagination;
using JHipsterNet.Core.Pagination.Extensions;
using Jhipster.Domain;
using Jhipster.Domain.Repositories.Interfaces;
using Jhipster.Infrastructure.Data.Extensions;
using System;
using Nest;
using Jhipster.Infrastructure.Data;
using System.Linq.Expressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Query;
using Newtonsoft.Json.Linq;

namespace Jhipster.Infrastructure.Data.Repositories
{
    public class BirthdayRepository : GenericRepository<Birthday>, IBirthdayRepository
    {
        private static Uri node = new Uri("https://texttemplate-testing-7087740692.us-east-1.bonsaisearch.net/");
        private static Nest.ConnectionSettings setting = new Nest.ConnectionSettings(node).BasicAuthentication("7303xa0iq9", "4cdkz0o14").DefaultIndex("birthdays");
        private static ElasticClient elastic = new ElasticClient(setting);
        public BirthdayRepository(IUnitOfWork context) : base(context)
        {
        }

        static Dictionary<string, List<string>> refKeys = new Dictionary<string, List<string>>{
            {"Hank Aaron",  new List<string>{"Hank Aaron"}},
            {"Claudio Abbado",  new List<string>{"Claudio Abbado"}},
            {"Mahmoud Abbas",  new List<string>{"Mahmoud Abbas"}},
            {"Kareem Abdul-Jabbar",  new List<string>{"Kareem Abdul-Jabbar"}},
            {"King of Jordan Abdullah II",  new List<string>{"King Abdullah"}},
            {"Jack Abramoff",  new List<string>{"Jack Abramoff"}},
            {"Floyd Abrams",  new List<string>{"Floyd Abrams"}},
            {"Jill Abramson",  new List<string>{"Jill Abramson"}},
            {"John Quincy Adams",  new List<string>{"John Quincy Adams"}},
            {"Ben Affleck",  new List<string>{"Ben Affleck"}},
            {"Andre Agassi",  new List<string>{"Andre Agassi"}},
            {"Spiro T Agnew",  new List<string>{"Spiro Agnew"}},
            {"Danny Aiello",  new List<string>{"Danny Aiello"}},
            {"Roberto Alagna",  new List<string>{"Roberto Alagna"}},
            {"Edward Albee",  new List<string>{"Edward Albee"}},
            {"Marv Albert",  new List<string>{"Marv Albert"}},
            {"Madeleine K Albright",  new List<string>{"Madeleine Albright"}},
            {"Louisa May Alcott",  new List<string>{"Louisa May Alcott"}},
            {"Alan Alda",  new List<string>{"Alan Alda"}},
            {"Jane Alexander",  new List<string>{"Jane Alexander"}},
            {"Jason Alexander",  new List<string>{"Jason Alexander"}},
            {"Lamar Alexander",  new List<string>{"Lamar Alexander"}},
            {"Muhammad Ali",  new List<string>{"Muhammad Ali"}},
            {"Samuel A Jr Alito",  new List<string>{"Samuel Alito"}},
            {"Paul Allen",  new List<string>{"Paul Allen"}},
            {"Tim Allen",  new List<string>{"Tim Allen"}},
            {"Woody Allen",  new List<string>{"Woody Allen"}},
            {"Isabel Allende",  new List<string>{"Isabel Allende"}},
            {"Salvador Allende Gossens",  new List<string>{"Salvador Allende"}},
            {"Pedro Almodovar",  new List<string>{"Pedro Almodovar"}},
            {"Marin Alsop",  new List<string>{"Marin Alsop"}},
            {"Christiane Amanpour",  new List<string>{"Christiane Amanpour"}},
            {"Aldrich Hazen Ames",  new List<string>{"Aldrich Ames"}},
            {"Hans Christian Andersen",  new List<string>{"Hans Christian Andersen"}},
            {"Wes Anderson",  new List<string>{"Wes Anderson"}},
            {"Julie Andrews",  new List<string>{"Julie Andrews"}},
            {"Maya Angelou",  new List<string>{"Maya Angelou"}},
            {"Jennifer Aniston",  new List<string>{"Jennifer Aniston"}},
            {"Walter H Annenberg",  new List<string>{"Walter Annenberg"}},
            {"Michelangelo Antonioni",  new List<string>{"Michelangelo"}},
            {"Judd Apatow",  new List<string>{"Judd Apatow"}},
            {"Yasir Arafat",  new List<string>{"Yasir Arafat"}},
            {"Alan Arkin",  new List<string>{"Alan Arkin"}},
            {"Giorgio Armani",  new List<string>{"Giorgio Armani"}},
            {"Dick Armey",  new List<string>{"Dick Armey"}},
            {"Lance Armstrong",  new List<string>{"Lance Armstrong"}},
            {"Louis Armstrong",  new List<string>{"Louis Armstrong"}},
            {"Peter Arnett",  new List<string>{"Peter Arnett"}},
            {"John Ashcroft",  new List<string>{"John Ashcroft"}},
            {"Arthur Ashe",  new List<string>{"Arthur Ashe"}},
            {"Hafez Al- Assad",  new List<string>{"Hafez Al- Assad"}},
            {"Fred Astaire",  new List<string>{"Fred Astaire"}},
            {"Margaret Atwood",  new List<string>{"Margaret Atwood"}},
            {"John James Audubon",  new List<string>{"Audubon"}},
            {"Emanuel Ax",  new List<string>{"Emanuel Ax"}},
            {"David Axelrod",  new List<string>{"David Axelrod"}},
            {"Alan Ayckbourn",  new List<string>{"Alan Ayckbourn"}},
            {"Bruce Babbitt",  new List<string>{"Bruce Babbitt"}},
            {"Lauren Bacall",  new List<string>{"Lauren Bacall"}},
            {"Johann Sebastian Bach",  new List<string>{"Bach"}},
            {"Burt Bacharach",  new List<string>{"Burt Bacharach"}},
            {"Kevin Bacon",  new List<string>{"Kevin Bacon"}},
            {"Joan Baez",  new List<string>{"Joan Baez"}},
            {"F Lee Bailey",  new List<string>{"F Lee Bailey"}},
            {"Howard H Jr Baker",  new List<string>{"Howard Baker"}},
            {"James A III Baker",  new List<string>{"James A Baker"}},
            {"George Balanchine",  new List<string>{"George Balanchine"}},
            {"Letitia Baldrige",  new List<string>{"Letitia Baldrige"}},
            {"Alec Baldwin",  new List<string>{"Alec Baldwin"}},
            {"William Baldwin",  new List<string>{"William Baldwin"}},
            {"Christian Bale",  new List<string>{"Christian Bale"}},
            {"Lucille Ball",  new List<string>{"Lucille Ball"}},
            {"Steven A Ballmer",  new List<string>{"Steve Ballmer"}},
            {" Ban Ki-moon",  new List<string>{"Ban Ki-moon"}},
            {"Antonio Banderas",  new List<string>{"Antonio Banderas"}},
            {"Ehud Barak",  new List<string>{"Ehud Barak"}},
            {"Christine Baranski",  new List<string>{"Christine Baranski"}},
            {"Samuel Barber",  new List<string>{"Samuel Barber"}},
            {"Haley Barbour",  new List<string>{"Haley Barbour"}},
            {"Javier Bardem",  new List<string>{"Javier Bardem"}},
            {"Brigitte Bardot",  new List<string>{"Brigitte Bardot"}},
            {"Daniel Barenboim",  new List<string>{"Daniel Barenboim"}},
            {"Charles Barkley",  new List<string>{"Charles Barkley"}},
            {"Dave Barry",  new List<string>{"Dave Barry"}},
            {"Marion S Jr Barry",  new List<string>{"Marion Barry"}},
            {"Drew Barrymore",  new List<string>{"Drew Barrymore"}},
            {"Cecilia Bartoli",  new List<string>{"Cecilia Bartoli"}},
            {"Mikhail Baryshnikov",  new List<string>{"Mikhail Baryshnikov"}},
            {"Kim Basinger",  new List<string>{"Kim Basinger"}},
            {"Kathy Bates",  new List<string>{"Kathy Bates"}},
            {"Kathleen Battle",  new List<string>{"Kathleen Battle"}},
            {"Max Baucus",  new List<string>{"Max Baucus"}},
            {"Evan Bayh",  new List<string>{"Evan Bayh"}},
            {"Abraham D Beame",  new List<string>{"Abraham Beame","Abe Beame"}},
            {"Milt Bearden",  new List<string>{"Milt Bearden"}},
            {"Warren Beatty",  new List<string>{"Warren Beatty"}},
            {"Samuel Beckett",  new List<string>{"Samuel Beckett"}},
            {"David Beckham",  new List<string>{"David Beckham"}},
            {"Kate Beckinsale",  new List<string>{"Kate Beckinsale"}},
            {"Ludwig van Beethoven",  new List<string>{"Beethoven"}},
            {"Paul Begala",  new List<string>{"Paul Begala"}},
            {"Harry Belafonte",  new List<string>{"Harry Belafonte"}},
            {"Bill Belichick",  new List<string>{"Bill Belichick"}},
            {"Joshua Bell",  new List<string>{"Joshua Bell"}},
            {"Saul Bellow",  new List<string>{"Saul Bellow"}},
            {" Benedict XVI",  new List<string>{" Pope Benedict"}},
            {"Roberto Benigni",  new List<string>{"Roberto Benigni"}},
            {"Annette Bening",  new List<string>{"Annette Bening"}},
            {"Robert S Bennett",  new List<string>{"Robert Bennett"}},
            {"William J Bennett",  new List<string>{"William Bennett"}},
            {"Alban Berg",  new List<string>{"Alban Berg"}},
            {"Candice Bergen",  new List<string>{"Candice Bergen"}},
            {"Samuel R Berger",  new List<string>{"Samuel Berger"}},
            {"Ingmar Bergman",  new List<string>{"Ingmar Bergman"}},
            {"Milton Berle",  new List<string>{"Milton Berle"}},
            {"Irving Berlin",  new List<string>{"Irving Berlin"}},
            {"Hector Berlioz",  new List<string>{"Berlioz"}},
            {"Silvio Berlusconi",  new List<string>{"Silvio Berlusconi"}},
            {"Ben S Bernanke",  new List<string>{"Ben S Bernanke"}},
            {"Carl Bernstein",  new List<string>{"Carl Bernstein"}},
            {"Leonard Bernstein",  new List<string>{"Leonard Bernstein"}},
            {"Yogi Berra",  new List<string>{"Yogi Berra"}},
            {"Chuck Berry",  new List<string>{"Chuck Berry"}},
            {"Halle Berry",  new List<string>{"Halle Berry"}},
            {"Jeffrey P Bezos",  new List<string>{"Jeffrey Bezos","Jeff Bezos"}},
            {"Benazir Bhutto",  new List<string>{"Benazir Bhutto"}},
            {"Joseph R Jr Biden",  new List<string>{"Joseph Biden"}},
            {"Osama Bin Laden",  new List<string>{"Osama Bin Laden"}},
            {"Rudolf Bing",  new List<string>{"Rudolf Bing"}},
            {"Juliette Binoche",  new List<string>{"Juliette Binoche"}},
            {"Larry Bird",  new List<string>{"Larry Bird"}},
            {"Georges Bizet",  new List<string>{"Georges Bizet"}},
            {"Harry A Blackmun",  new List<string>{"Harry Blackmun"}},
            {"Rod R Blagojevich",  new List<string>{"Rod Blagojevich"}},
            {"Tony Blair",  new List<string>{"Tony Blair"}},
            {"Cate Blanchett",  new List<string>{"Cate Blanchett"}},
            {"Mary J Blige",  new List<string>{"Mary J Blige"}},
            {"Claire Bloom",  new List<string>{"Claire Bloom"}},
            {"Michael R Bloomberg",  new List<string>{"Michael Bloomberg"}},
            {"Judy Blume",  new List<string>{"Judy Blume"}},
            {"Andrea Bocelli",  new List<string>{"Andrea Bocelli"}},
            {"Humphrey Bogart",  new List<string>{"Humphrey Bogart"}},
            {"Peter Bogdanovich",  new List<string>{"Bogdanovich"}},
            {"Wade Boggs",  new List<string>{"Wade Boggs"}},
            {"John R Bolton",  new List<string>{"John Bolton"}},
            {"Jon Bon Jovi",  new List<string>{"Jon Bon Jovi"}},
            {"Julian Bond",  new List<string>{"Julian Bond"}},
            {"Sonny Bono",  new List<string>{"Sonny Bono"}},
            {" Bono",  new List<string>{"Bono"}},
            {"Max Boot",  new List<string>{"Max Boot"}},
            {"Victor Borge",  new List<string>{"Victor Borge"}},
            {"Robert H Bork",  new List<string>{"Robert H Bork"}},
            {"P W Botha",  new List<string>{"P W Botha"}},
            {"Kathy Boudin",  new List<string>{"Kathy Boudin"}},
            {"Pierre Boulez",  new List<string>{"Pierre Boulez"}},
            {"David Bowie",  new List<string>{"David Bowie"}},
            {"Barbara Boxer",  new List<string>{"Barbara Boxer"}},
            {"Danny Boyle",  new List<string>{"Danny Boyle"}},
            {"Ray Bradbury",  new List<string>{"Ray Bradbury"}},
            {"Bill Bradley",  new List<string>{"Bill Bradley"}},
            {"James S Brady",  new List<string>{"James Brady"}},
            {"Tom Brady",  new List<string>{"Tom Brady"}},
            {"Johannes Brahms",  new List<string>{"Brahms"}},
            {"Kenneth Branagh",  new List<string>{"Kenneth Branagh"}},
            {"Marlon Brando",  new List<string>{"Marlon Brando"}},
            {"Richard Branson",  new List<string>{"Richard Branson"}},
            {"Bertolt Brecht",  new List<string>{"Bertolt Brecht"}},
            {"Stephen G Breyer",  new List<string>{"Stephen Breyer"}},
            {"Jeff Bridges",  new List<string>{"Jeff Bridges"}},
            {"Sergey Brin",  new List<string>{"Sergey Brin"}},
            {"David Brinkley",  new List<string>{"David Brinkley"}},
            {"Douglas Brinkley",  new List<string>{"Douglas Brinkley"}},
            {"Benjamin Britten",  new List<string>{"Benjamin Britten"}},
            {"Matthew Broderick",  new List<string>{"Matthew Broderick"}},
            {"Joseph Brodsky",  new List<string>{"Joseph Brodsky"}},
            {"Adrien Brody",  new List<string>{"Adrien Brody"}},
            {"Jane E Brody",  new List<string>{"Jane Brody"}},
            {"Albert Brooks",  new List<string>{"Albert Brooks"}},
            {"Garth Brooks",  new List<string>{"Garth Brooks"}},
            {"Mel Brooks",  new List<string>{"Mel Brooks"}},
            {"Pierce Brosnan",  new List<string>{"Pierce Brosnan"}},
            {"James Brown",  new List<string>{"James Brown"}},
            {"Jason Robert Brown",  new List<string>{"Jason Robert Brown"}},
            {"Tina Brown",  new List<string>{"Tina Brown"}},
            {"Sam Brownback",  new List<string>{"Sam Brownback"}},
            {"Lenny Bruce",  new List<string>{"Lenny Bruce"}},
            {"Jerry Bruckheimer",  new List<string>{"Jerry Bruckheimer"}},
            {"Frank Bruni",  new List<string>{"Frank Bruni"}},
            {"Kobe Bryant",  new List<string>{"Kobe Bryant"}},
            {"Art Buchwald",  new List<string>{"Art Buchwald"}},
            {"Betty Buckley",  new List<string>{"Betty Buckley"}},
            {"Jimmy Buffett",  new List<string>{"Jimmy Buffett"}},
            {"Warren E Buffett",  new List<string>{"Warren Buffett"}},
            {"Sandra Bullock",  new List<string>{"Sandra Bullock"}},
            {"Dale Bumpers",  new List<string>{"Dale Bumpers"}},
            {"Carol Burnett",  new List<string>{"Carol Burnett"}},
            {"Mark Burnett",  new List<string>{"Mark Burnett"}},
            {"Edward Burns",  new List<string>{"Edward Burns"}},
            {"George Burns",  new List<string>{"George Burns"}},
            {"Ken Burns",  new List<string>{"Ken Burns"}},
            {"Aaron Burr",  new List<string>{"Aaron Burr"}},
            {"William S Burroughs",  new List<string>{"William Burroughs"}},
            {"Ellen Burstyn",  new List<string>{"Ellen Burstyn"}},
            {"Dan Burton",  new List<string>{"Dan Burton"}},
            {"Tim Burton",  new List<string>{"Tim Burton"}},
            {"Steve Buscemi",  new List<string>{"Steve Buscemi"}},
            {"Barbara Bush",  new List<string>{"Barbara Bush"}},
            {"George Bush",  new List<string>{"George Bush"}},
            {"George W Bush",  new List<string>{"George W Bush"}},
            {"Jeb Bush",  new List<string>{"Jeb Bush"}},
            {"Jenna Bush",  new List<string>{"Jenna Bush"}},
            {"Laura Bush",  new List<string>{"Laura Bush"}},
            {"Neil Bush",  new List<string>{"Neil Bush"}},
            {"Robert C Byrd",  new List<string>{"Robert Byrd"}},
            {"Sid Caesar",  new List<string>{"Sid Caesar"}},
            {"John Cage",  new List<string>{"John Cage"}},
            {"Nicolas Cage",  new List<string>{"Nicolas Cage"}},
            {"Michael Caine",  new List<string>{"Michael Caine"}},
            {"Alexander Calder",  new List<string>{"Alexander Calder"}},
            {"Felipe Calderon",  new List<string>{"Felipe Calderon"}},
            {"Maria Callas",  new List<string>{"Maria Callas"}},
            {"David Cameron",  new List<string>{"David Cameron"}},
            {"James Cameron",  new List<string>{"James Cameron"}},
            {"Jane Campion",  new List<string>{"Jane Campion"}},
            {"Albert Camus",  new List<string>{"Albert Camus"}},
            {"Jose Canseco",  new List<string>{"Jose Canseco"}},
            {"Truman Capote",  new List<string>{"Truman Capote"}},
            {"Andrew H Jr Card",  new List<string>{"Andrew Card"}},
            {"Steve Carell",  new List<string>{"Steve Carell"}},
            {"Hugh L Carey",  new List<string>{"Hugh Carey"}},
            {"Mariah Carey",  new List<string>{"Mariah Carey"}},
            {"George Carlin",  new List<string>{"George Carlin"}},
            {"Tucker Carlson",  new List<string>{"Tucker Carlson"}},
            {"Andrew Carnegie",  new List<string>{"Andrew Carnegie"}},
            {"David Carr",  new List<string>{"David Carr"}},
            {"Jim Carrey",  new List<string>{"Jim Carrey"}},
            {"Johnny Carson",  new List<string>{"Johnny Carson"}},
            {"Benny Carter",  new List<string>{"Benny Carter"}},
            {"Graydon Carter",  new List<string>{"Graydon Carter"}},
            {"Jimmy Carter",  new List<string>{"Jimmy Carter"}},
            {"Rosalynn Carter",  new List<string>{"Rosalynn Carter"}},
            {"Enrico Caruso",  new List<string>{"Caruso"}},
            {"James Carville",  new List<string>{"James Carville"}},
            {"Stephen M Case",  new List<string>{"Stephen Case","Steve Case"}},
            {"Johnny Cash",  new List<string>{"Johnny Cash"}},
            {"Mary Cassatt",  new List<string>{"Mary Cassatt"}},
            {"Fidel Castro",  new List<string>{"Fidel Castro"}},
            {"Willa Cather",  new List<string>{"Willa Cather"}},
            {"Dick Cavett",  new List<string>{"Dick Cavett"}},
            {"John H Chafee",  new List<string>{"John Chafee"}},
            {"Lincoln Chafee",  new List<string>{"Lincoln Chafee"}},
            {"Marc Chagall",  new List<string>{"Marc Chagall"}},
            {"Ahmad Chalabi",  new List<string>{"Ahmad Chalabi"}},
            {"Wilt Chamberlain",  new List<string>{"Wilt Chamberlain"}},
            {"Saxby Chambliss",  new List<string>{"Saxby Chambliss"}},
            {"Jackie Chan",  new List<string>{"Jackie Chan"}},
            {"Raymond Chandler",  new List<string>{"Raymond Chandler"}},
            {"Carol Channing",  new List<string>{"Carol Channing"}},
            {"Stockard Channing",  new List<string>{"Stockard Channing"}},
            {"Elaine L Chao",  new List<string>{"Elaine Chao"}},
            {"Charlie Chaplin",  new List<string>{"Charlie Chaplin"}},
            {"Ray Charles",  new List<string>{"Ray Charles"}},
            {"Chevy Chase",  new List<string>{"Chevy Chase"}},
            {"Julio Cesar Chavez",  new List<string>{"Julio Cesar Chavez"}},
            {"Linda Chavez",  new List<string>{"Linda Chavez"}},
            {"Don Cheadle",  new List<string>{"Don Cheadle"}},
            {"John Cheever",  new List<string>{"John Cheever"}},
            {"Susan Cheever",  new List<string>{"Susan Cheever"}},
            {"Anton Chekhov",  new List<string>{"Anton Chekhov"}},
            {"Dick Cheney",  new List<string>{"Dick Cheney"}},
            {"Lynne V Cheney",  new List<string>{"Lynne Cheney"}},
            {"Kristin Chenoweth",  new List<string>{"Kristin Chenoweth"}},
            {" Cher",  new List<string>{"Cher"}},
            {"Ron Chernow",  new List<string>{"Ron Chernow"}},
            {"Michael Chertoff",  new List<string>{"Michael Chertoff"}},
            {"Julia Child",  new List<string>{"Julia Child"}},
            {"Noam Chomsky",  new List<string>{"Noam Chomsky"}},
            {"Frederic Chopin",  new List<string>{"Chopin"}},
            {"Agatha Christie",  new List<string>{"Agatha Christie"}},
            {"Warren M Christopher",  new List<string>{"Warren Christopher"}},
            {"Connie Chung",  new List<string>{"Connie Chung"}},
            {"Winston Leonard Spencer Churchill",  new List<string>{"Winston Churchill"}},
            {"Craig Claiborne",  new List<string>{"Craig Claiborne"}},
            {"Eric Clapton",  new List<string>{"Eric Clapton"}},
            {"Dick Clark",  new List<string>{"Dick Clark"}},
            {"Wesley K Clark",  new List<string>{"Wesley Clark"}},
            {"Arthur C Clarke",  new List<string>{"Arthur Clarke"}},
            {"Richard A Clarke",  new List<string>{"Richard Clarke"}},
            {"Kelly Clarkson",  new List<string>{"Kelly Clarkson"}},
            {"Joan Claybrook",  new List<string>{"Joan Claybrook"}},
            {"Max Cleland",  new List<string>{"Max Cleland"}},
            {"Roger Clemens",  new List<string>{"Roger Clemens"}},
            {"Samuel Langhorne Clemens",  new List<string>{"Samuel Langhorne Clemens","Mark Twain"}},
            {"Van Cliburn",  new List<string>{"Van Cliburn"}},
            {"Bill Clinton",  new List<string>{"Bill Clinton"}},
            {"Chelsea Clinton",  new List<string>{"Chelsea Clinton"}},
            {"Hillary Rodham Clinton",  new List<string>{"Hillary Clinton"}},
            {"George Clooney",  new List<string>{"George Clooney"}},
            {"Rosemary Clooney",  new List<string>{"Rosemary Clooney"}},
            {"Glenn Close",  new List<string>{"Glenn Close"}},
            {"Kurt Cobain",  new List<string>{"Kurt Cobain"}},
            {"Ty Cobb",  new List<string>{"Ty Cobb"}},
            {"Tom Coburn",  new List<string>{"Tom Coburn"}},
            {"Johnnie L Jr Cochran",  new List<string>{"Johnnie Cochran"}},
            {"Thad Cochran",  new List<string>{"Thad Cochran"}},
            {"Jean Cocteau",  new List<string>{"Jean Cocteau"}},
            {"Leonard Cohen",  new List<string>{"Leonard Cohen"}},
            {"Roger Cohen",  new List<string>{"Roger Cohen"}},
            {"William S Cohen",  new List<string>{"William Cohen"}},
            {"Stephen Colbert",  new List<string>{"Stephen Colbert"}},
            {"William E Colby",  new List<string>{"William Colby"}},
            {"Joan Collins",  new List<string>{"Joan Collins"}},
            {"Judy Collins",  new List<string>{"Judy Collins"}},
            {"Phil Collins",  new List<string>{"Phil Collins"}},
            {"Christopher Columbus",  new List<string>{"Columbus"}},
            {"Betty Comden",  new List<string>{"Betty Comden"}},
            {"James B Comey",  new List<string>{"James Comey"}},
            {"Sean Connery",  new List<string>{"Sean Connery"}},
            {"Jimmy Connors",  new List<string>{"Jimmy Connors"}},
            {"John Jr Conyers",  new List<string>{"John Conyers"}},
            {"Barbara Cook",  new List<string>{"Barbara Cook"}},
            {"Alistair Cooke",  new List<string>{"Alistair Cooke"}},
            {"Jack Kent Cooke",  new List<string>{"Jack Kent Cooke"}},
            {"Aaron Copland",  new List<string>{"Aaron Copland"}},
            {"Francis Ford Coppola",  new List<string>{"Francis Ford Coppola"}},
            {"Sofia Coppola",  new List<string>{"Sofia Coppola"}},
            {"Chick Corea",  new List<string>{"Chick Corea"}},
            {"Joseph Cornell",  new List<string>{"Joseph Cornell"}},
            {"John Cornyn",  new List<string>{"John Cornyn"}},
            {"Bill Cosby",  new List<string>{"Bill Cosby"}},
            {"Bob Costas",  new List<string>{"Bob Costas"}},
            {"Elvis Costello",  new List<string>{"Elvis Costello"}},
            {"Kevin Costner",  new List<string>{"Kevin Costner"}},
            {"Noel Coward",  new List<string>{"Noel Coward"}},
            {"Simon Cowell",  new List<string>{"Simon Cowell"}},
            {"Archibald Cox",  new List<string>{"Archibald Cox"}},
            {"Gregory B Craig",  new List<string>{"Gregory Craig","Greg Craig"}},
            {"James J Cramer",  new List<string>{"James Cramer"}},
            {"Michael Crichton",  new List<string>{"Michael Crichton"}},
            {"Walter Cronkite",  new List<string>{"Walter Cronkite"}},
            {"Hume Cronyn",  new List<string>{"Hume Cronyn"}},
            {"Bing Crosby",  new List<string>{"Bing Crosby"}},
            {"Sheryl Crow",  new List<string>{"Sheryl Crow"}},
            {"Tom Cruise",  new List<string>{"Tom Cruise"}},
            {"Penelope Cruz",  new List<string>{"Penelope Cruz"}},
            {"Billy Crystal",  new List<string>{"Billy Crystal"}},
            {"Alfonso Cuaron",  new List<string>{"Alfonso Cuaron"}},
            {"Mark Cuban",  new List<string>{"Mark Cuban"}},
            {"Macaulay Culkin",  new List<string>{"Macaulay Culkin"}},
            {"Alan Cumming",  new List<string>{"Alan Cumming"}},
            {"Andrew M Cuomo",  new List<string>{"Andrew Cuomo"}},
            {"Mario M Cuomo",  new List<string>{"Mario Cuomo"}},
            {"Jamie Lee Curtis",  new List<string>{"Jamie Lee Curtis"}},
            {"John Cusack",  new List<string>{"John Cusack"}},
            {"George Armstrong Custer",  new List<string>{"Custer"}},
            {"Miley Cyrus",  new List<string>{"Miley Cyrus"}},
            {"Leonardo da Vinci",  new List<string>{"da Vinci"}},
            {"Willem Dafoe",  new List<string>{"Willem Dafoe"}},
            {" Dalai Lama",  new List<string>{"Dalai Lama"}},
            {"William M Daley",  new List<string>{"William Daley"}},
            {"Salvador Dali",  new List<string>{"Salvador Dali"}},
            {"Matt Damon",  new List<string>{"Matt Damon"}},
            {"Claire Danes",  new List<string>{"Claire Danes"}},
            {"John C Danforth",  new List<string>{"John Danforth"}},
            {"Jeff Daniels",  new List<string>{"Jeff Daniels"}},
            {"Blythe Danner",  new List<string>{"Blythe Danner"}},
            {"Ted Danson",  new List<string>{"Ted Danson"}},
            {"Charles Robert Darwin",  new List<string>{"Darwin"}},
            {"Tom Daschle",  new List<string>{"Tom Daschle"}},
            {"Samuel Dash",  new List<string>{"Samuel Dash"}},
            {"Larry David",  new List<string>{"Larry David"}},
            {"Bette Davis",  new List<string>{"Bette Davis"}},
            {"Gray Davis",  new List<string>{"Gray Davis"}},
            {"Miles Davis",  new List<string>{"Miles Davis"}},
            {"Ossie Davis",  new List<string>{"Ossie Davis"}},
            {"Richard Dawkins",  new List<string>{"Richard Dawkins"}},
            {"Daniel Day-Lewis",  new List<string>{"Daniel Day-Lewis"}},
            {"Bill de Blasio",  new List<string>{"Bill de Blasio"}},
            {"Charles de Gaulle",  new List<string>{"de Gaulle"}},
            {"F W de Klerk",  new List<string>{"F W de Klerk"}},
            {"Willem de Kooning",  new List<string>{"de Kooning"}},
            {"Oscar de La Hoya",  new List<string>{"Oscar de La Hoya"}},
            {"Oscar de la Renta",  new List<string>{"Oscar de la Renta"}},
            {"Robert De Niro",  new List<string>{"Robert De Niro"}},
            {"Brian de Palma",  new List<string>{"Brian de Palma"}},
            {"Howard Dean",  new List<string>{"Howard Dean"}},
            {"James Dean",  new List<string>{"James Dean"}},
            {"Claude Debussy",  new List<string>{"Claude Debussy"}},
            {"Ruby Dee",  new List<string>{"Ruby Dee"}},
            {"Edgar Degas",  new List<string>{"Degas"}},
            {"Ellen DeGeneres",  new List<string>{"Ellen DeGeneres"}},
            {"Benicio Del Toro",  new List<string>{"Benicio Del Toro"}},
            {"Tom Delay",  new List<string>{"Tom Delay"}},
            {"Michael S Dell",  new List<string>{"Michael Dell"}},
            {"Judi Dench",  new List<string>{"Judi Dench"}},
            {" Deng Xiaoping",  new List<string>{"Deng Xiaoping"}},
            {"John Denver",  new List<string>{"John Denver"}},
            {"Gerard Depardieu",  new List<string>{"Gerard Depardieu"}},
            {"Johnny Depp",  new List<string>{"Johnny Depp"}},
            {"Alan M Dershowitz",  new List<string>{"Alan Dershowitz"}},
            {"John M Deutch",  new List<string>{"John M Deutch"}},
            {"Donny Deutsch",  new List<string>{"Donny Deutsch"}},
            {"Danny DeVito",  new List<string>{"Danny DeVito"}},
            {"Colleen Dewhurst",  new List<string>{"Colleen Dewhurst"}},
            {"Mike DeWine",  new List<string>{"Mike DeWine"}},
            {"Neil Diamond",  new List<string>{"Neil Diamond"}},
            {"Cameron Diaz",  new List<string>{"Cameron Diaz"}},
            {"Leonardo DiCaprio",  new List<string>{"Leonardo DiCaprio"}},
            {"Philip K Dick",  new List<string>{"Philip K Dick"}},
            {"Charles Dickens",  new List<string>{"Charles Dickens"}},
            {"Emily Dickinson",  new List<string>{"Emily Dickinson"}},
            {"Joan Didion",  new List<string>{"Joan Didion"}},
            {"Marlene Dietrich",  new List<string>{"Marlene Dietrich"}},
            {"Barry Diller",  new List<string>{"Barry Diller"}},
            {"Joe DiMaggio",  new List<string>{"Joe DiMaggio"}},
            {"James Dimon",  new List<string>{"James Dimon"}},
            {"John D Dingell",  new List<string>{"John Dingell"}},
            {"David N Dinkins",  new List<string>{"David Dinkins"}},
            {"Celine Dion",  new List<string>{"Celine Dion"}},
            {"Roy E Disney",  new List<string>{"Roy Disney"}},
            {"Walt Disney",  new List<string>{"Walt Disney"}},
            {"Mike Ditka",  new List<string>{"Mike Ditka"}},
            {"Lou Dobbs",  new List<string>{"Lou Dobbs"}},
            {"E L Doctorow",  new List<string>{"E L Doctorow"}},
            {"Christopher J Dodd",  new List<string>{"Christopher Dodd"}},
            {"Bob Dole",  new List<string>{"Bob Dole"}},
            {"Elizabeth Dole",  new List<string>{"Elizabeth Dole"}},
            {"Pete V Domenici",  new List<string>{"Pete Domenici"}},
            {"Placido Domingo",  new List<string>{"Placido Domingo"}},
            {"Phil Donahue",  new List<string>{"Phil Donahue"}},
            {"Sam Donaldson",  new List<string>{"Sam Donaldson"}},
            {"Fyodor Dostoyevsky",  new List<string>{"Dostoyevsky"}},
            {"Kirk Douglas",  new List<string>{"Kirk Douglas"}},
            {"Michael Douglas",  new List<string>{"Michael Douglas"}},
            {"Maureen Dowd",  new List<string>{"Maureen Dowd"}},
            {"Arthur Conan Doyle",  new List<string>{"Arthur Conan Doyle"}},
            {"Richard Dreyfuss",  new List<string>{"Richard Dreyfuss"}},
            {"Matt Drudge",  new List<string>{"Matt Drudge"}},
            {"John E Du Pont",  new List<string>{"John Du Pont"}},
            {"David Duchovny",  new List<string>{"David Duchovny"}},
            {"Michael S Dukakis",  new List<string>{"Michael Dukakis"}},
            {"Olympia Dukakis",  new List<string>{"Olympia Dukakis"}},
            {"David E Duke",  new List<string>{"David Duke"}},
            {"Doris Duke",  new List<string>{"Doris Duke"}},
            {"Faye Dunaway",  new List<string>{"Faye Dunaway"}},
            {"Arne Duncan",  new List<string>{"Arne Duncan"}},
            {"Isadora Duncan",  new List<string>{"Isadora Duncan"}},
            {"Dominick Dunne",  new List<string>{"Dominick Dunne"}},
            {"John Gregory Dunne",  new List<string>{"John Dunne"}},
            {"Richard J Durbin",  new List<string>{"Richard Durbin"}},
            {"Charles Durning",  new List<string>{"Charles Durning"}},
            {"Charles Dutoit",  new List<string>{"Charles Dutoit"}},
            {"Robert Duvall",  new List<string>{"Robert Duvall"}},
            {"Bob Dylan",  new List<string>{"Bob Dylan"}},
            {"Esther Dyson",  new List<string>{"Esther Dyson"}},
            {"Lawrence S Eagleburger",  new List<string>{"Lawrence Eagleburger"}},
            {"Amelia Earhart",  new List<string>{"Amelia Earhart"}},
            {"Clint Eastwood",  new List<string>{"Clint Eastwood"}},
            {"Dick Ebersol",  new List<string>{"Dick Ebersol"}},
            {"Christine Ebersole",  new List<string>{"Christine Ebersole"}},
            {"Thomas A Edison",  new List<string>{"Thomas Edison"}},
            {"Blake Edwards",  new List<string>{"Blake Edwards"}},
            {"John Edwards",  new List<string>{"John Edwards"}},
            {"Barbara Ehrenreich",  new List<string>{"Barbara Ehrenreich"}},
            {"Adolf Eichmann",  new List<string>{"Adolf Eichmann"}},
            {"Albert Einstein",  new List<string>{"Einstein"}},
            {"Dwight David Eisenhower",  new List<string>{"Eisenhower"}},
            {"Sergei Eisenstein",  new List<string>{"Sergei Eisenstein"}},
            {"Michael D Eisner",  new List<string>{"Michael Eisner"}},
            {"Stuart E Eizenstat",  new List<string>{"Stuart Eizenstat"}},
            {"Queen of Great Britain Elizabeth II",  new List<string>{"Queen Elizabeth II"}},
            {"Duke Ellington",  new List<string>{"Duke Ellington"}},
            {"Lawrence J Ellison",  new List<string>{"Lawrence Ellison","Larry Ellison"}},
            {"Daniel Ellsberg",  new List<string>{"Daniel Ellsberg"}},
            {"Rahm Emanuel",  new List<string>{"Rahm Emanuel"}},
            {" Eminem",  new List<string>{"Eminem"}},
            {"Nora Ephron",  new List<string>{"Nora Ephron"}},
            {"Recep Tayyip Erdogan",  new List<string>{"Erdogan"}},
            {"Louise Erdrich",  new List<string>{"Louise Erdrich"}},
            {"Christoph Eschenbach",  new List<string>{"Christoph Eschenbach"}},
            {"Gloria Estefan",  new List<string>{"Gloria Estefan"}},
            {"Medgar Evers",  new List<string>{"Medgar Evers"}},
            {"Chris Evert",  new List<string>{"Chris Evert"}},
            {"Patrick Ewing",  new List<string>{"Patrick Ewing"}},
            {"Marianne Faithfull",  new List<string>{"Marianne Faithfull"}},
            {"Edie Falco",  new List<string>{"Edie Falco"}},
            {"Peter Falk",  new List<string>{"Peter Falk"}},
            {"James Fallows",  new List<string>{"James Fallows"}},
            {"Jerry Falwell",  new List<string>{"Jerry Falwell"}},
            {"Louis Farrakhan",  new List<string>{"Louis Farrakhan"}},
            {"Colin Farrell",  new List<string>{"Colin Farrell"}},
            {"Mia Farrow",  new List<string>{"Mia Farrow"}},
            {"Anthony S Fauci",  new List<string>{"Anthony Fauci"}},
            {"William Faulkner",  new List<string>{"William Faulkner"}},
            {"Brett Favre",  new List<string>{"Brett Favre"}},
            {"Mohamed al- Fayed",  new List<string>{"Mohamed al-Fayed"}},
            {"Jules Feiffer",  new List<string>{"Jules Feiffer"}},
            {"Kenneth R Feinberg",  new List<string>{"Kenneth Feinberg"}},
            {"Russell D Feingold",  new List<string>{"Russell Feingold","Russ Feingold"}},
            {"Dianne Feinstein",  new List<string>{"Dianne Feinstein"}},
            {"Federico Fellini",  new List<string>{"Federico Fellini"}},
            {"Niall Ferguson",  new List<string>{"Niall Ferguson"}},
            {"Geraldine A Ferraro",  new List<string>{"Geraldine Ferraro"}},
            {"Will Ferrell",  new List<string>{"Will Ferrell"}},
            {"Tina Fey",  new List<string>{"Tina Fey"}},
            {"Sally Field",  new List<string>{"Sally Field"}},
            {"Ralph Fiennes",  new List<string>{"Ralph Fiennes"}},
            {"Harvey Fierstein",  new List<string>{"Harvey Fierstein"}},
            {"Carleton S Fiorina",  new List<string>{"Fiorina"}},
            {"Colin Firth",  new List<string>{"Colin Firth"}},
            {"Bobby Fischer",  new List<string>{"Bobby Fischer"}},
            {"Carrie Fisher",  new List<string>{"Carrie Fisher"}},
            {"Ella Fitzgerald",  new List<string>{"Ella Fitzgerald"}},
            {"F Scott Fitzgerald",  new List<string>{"F Scott Fitzgerald"}},
            {"Ari Fleischer",  new List<string>{"Ari Fleischer"}},
            {"Renee Fleming",  new List<string>{"Renee Fleming"}},
            {"Gennifer Flowers",  new List<string>{"Gennifer Flowers"}},
            {"Larry Flynt",  new List<string>{"Larry Flynt"}},
            {"Jane Fonda",  new List<string>{"Jane Fonda"}},
            {"Gerald Rudolph Jr Ford",  new List<string>{"Gerald Ford"}},
            {"Harrison Ford",  new List<string>{"Harrison Ford"}},
            {"Henry Ford",  new List<string>{"Henry Ford"}},
            {"Tom Ford",  new List<string>{"Tom Ford"}},
            {"George Foreman",  new List<string>{"George Foreman"}},
            {"Bob Fosse",  new List<string>{"Bob Fosse"}},
            {"Jodie Foster",  new List<string>{"Jodie Foster"}},
            {"Vincent W Jr Foster",  new List<string>{"Vincent Foster"}},
            {"Michael J Fox",  new List<string>{"Michael J Fox"}},
            {"Jamie Foxx",  new List<string>{"Jamie Foxx"}},
            {"Anne Frank",  new List<string>{"Anne Frank"}},
            {"Al Franken",  new List<string>{"Al Franken"}},
            {"Aretha Franklin",  new List<string>{"Aretha Franklin"}},
            {"Benjamin Franklin",  new List<string>{"Benjamin Franklin","Ben Franklin"}},
            {"Mirella Freni",  new List<string>{"Mirella Freni"}},
            {"Sigmund Freud",  new List<string>{"Sigmund Freud"}},
            {"Betty Friedan",  new List<string>{"Betty Friedan"}},
            {"Thomas L Friedman",  new List<string>{"Thomas Friedman"}},
            {"Fred W Friendly",  new List<string>{"Fred Friendly"}},
            {"Bill Frist",  new List<string>{"Bill Frist"}},
            {"David Frum",  new List<string>{"David Frum"}},
            {"Stephen Fry",  new List<string>{"Stephen Fry"}},
            {"Mark Fuhrman",  new List<string>{"Mark Fuhrman"}},
            {"John Kenneth Galbraith",  new List<string>{"John Kenneth Galbraith"}},
            {"James Galway",  new List<string>{"James Galway"}},
            {"Indira Gandhi",  new List<string>{"Indira Gandhi"}},
            {"James Gandolfini",  new List<string>{"James Gandolfini"}},
            {"Greta Garbo",  new List<string>{"Greta Garbo"}},
            {"Gil Garcetti",  new List<string>{"Gil Garcetti"}},
            {"Jerry Garcia",  new List<string>{"Jerry Garcia"}},
            {"Judy Garland",  new List<string>{"Judy Garland"}},
            {"Bill Gates",  new List<string>{"Bill Gates"}},
            {"Henry Louis Jr Gates",  new List<string>{"Henry Louis Gates"}},
            {"Melinda Gates",  new List<string>{"Melinda Gates"}},
            {"Robert M Gates",  new List<string>{"Robert Gates"}},
            {"Lou Gehrig",  new List<string>{"Lou Gehrig"}},
            {"Frank Gehry",  new List<string>{"Frank Gehry"}},
            {"Theodor Seuss Geisel",  new List<string>{"Seuss"}},
            {"Peter Gelb",  new List<string>{"Peter Gelb"}},
            {"Richard A Gephardt",  new List<string>{"Richard Gephardt"}},
            {"Julie L Gerberding",  new List<string>{"Julie Gerberding"}},
            {"Richard Gere",  new List<string>{"Richard Gere"}},
            {"George Gershwin",  new List<string>{"George Gershwin"}},
            {"Angela Gheorghiu",  new List<string>{"Angela Gheorghiu"}},
            {"Althea Gibson",  new List<string>{"Althea Gibson"}},
            {"Mel Gibson",  new List<string>{"Mel Gibson"}},
            {"John Gielgud",  new List<string>{"John Gielgud"}},
            {"Kathie Lee Gifford",  new List<string>{"Kathie Lee Gifford"}},
            {"Dizzy Gillespie",  new List<string>{"Dizzy Gillespie"}},
            {"Kirsten E Gillibrand",  new List<string>{"Kirsten Gillibrand"}},
            {"Newt Gingrich",  new List<string>{"Newt Gingrich"}},
            {"Allen Ginsberg",  new List<string>{"Allen Ginsberg"}},
            {"Ruth Bader Ginsburg",  new List<string>{"Ruth Bader Ginsburg"}},
            {"Lillian Gish",  new List<string>{"Lillian Gish"}},
            {"Rudolph W Giuliani",  new List<string>{"Rudolph W Giuliani"}},
            {"Philip Glass",  new List<string>{"Philip Glass"}},
            {"Jackie Gleason",  new List<string>{"Jackie Gleason"}},
            {"John Glenn",  new List<string>{"John Glenn"}},
            {"Danny Glover",  new List<string>{"Danny Glover"}},
            {"Jean-Luc Godard",  new List<string>{"Jean-Luc Godard"}},
            {"Whoopi Goldberg",  new List<string>{"Whoopi Goldberg"}},
            {"Barry M Goldwater",  new List<string>{"Barry Goldwater"}},
            {"Alberto R Gonzales",  new List<string>{"Alberto Gonzales"}},
            {"Jane Goodall",  new List<string>{"Jane Goodall"}},
            {"Roger Goodell",  new List<string>{"Roger Goodell"}},
            {"Benny Goodman",  new List<string>{"Benny Goodman"}},
            {"John Goodman",  new List<string>{"John Goodman"}},
            {"Doris Kearns Goodwin",  new List<string>{"Doris Kearns Goodwin"}},
            {"Mikhail S Gorbachev",  new List<string>{"Mikhail Gorbachev"}},
            {"Al Gore",  new List<string>{"Al Gore"}},
            {"Tipper Gore",  new List<string>{"Tipper Gore"}},
            {"Porter J Goss",  new List<string>{"Porter Goss"}},
            {"Glenn Gould",  new List<string>{"Glenn Gould"}},
            {"Stephen Jay Gould",  new List<string>{"Stephen Jay Gould"}},
            {"Francisco de Goya",  new List<string>{"Goya"}},
            {"Billy Graham",  new List<string>{"Billy Graham"}},
            {"Bob Graham",  new List<string>{"Bob Graham"}},
            {"Franklin Graham",  new List<string>{"Franklin Graham"}},
            {"Lindsey Graham",  new List<string>{"Lindsey Graham"}},
            {"Martha Graham",  new List<string>{"Martha Graham"}},
            {"Kelsey Grammer",  new List<string>{"Kelsey Grammer"}},
            {"Hugh Grant",  new List<string>{"Hugh Grant"}},
            {"Ulysses S Grant",  new List<string>{"Ulysses S Grant"}},
            {"Charles E Grassley",  new List<string>{"Charles Grassley"}},
            {"Spalding Gray",  new List<string>{"Spalding Gray"}},
            {"Adolph Green",  new List<string>{"Adolph Green"}},
            {"Graham Greene",  new List<string>{"Graham Greene"}},
            {"Alan Greenspan",  new List<string>{"Alan Greenspan"}},
            {"Wayne Gretzky",  new List<string>{"Wayne Gretzky"}},
            {"Merv Griffin",  new List<string>{"Merv Griffin"}},
            {"Melanie Griffith",  new List<string>{"Melanie Griffith"}},
            {"John Grisham",  new List<string>{"John Grisham"}},
            {"Andrew S Grove",  new List<string>{"Andrew Grove"}},
            {"Lani Guinier",  new List<string>{"Lani Guinier"}},
            {"Alec Guinness",  new List<string>{"Alec Guinness"}},
            {"Bryant Gumbel",  new List<string>{"Bryant Gumbel"}},
            {"Arlo Guthrie",  new List<string>{"Arlo Guthrie"}},
            {"Woody Guthrie",  new List<string>{"Woody Guthrie"}},
            {"Jake Gyllenhaal",  new List<string>{"Jake Gyllenhaal"}},
            {"Maggie Gyllenhaal",  new List<string>{"Maggie Gyllenhaal"}},
            {"Stephen J Hadley",  new List<string>{"Stephen Hadley"}},
            {"Chuck Hagel",  new List<string>{"Chuck Hagel"}},
            {"David Halberstam",  new List<string>{"David Halberstam"}},
            {"Arsenio Hall",  new List<string>{"Arsenio Hall"}},
            {"Pete Hamill",  new List<string>{"Pete Hamill"}},
            {"Alexander Hamilton",  new List<string>{"Alexander Hamilton"}},
            {"Darryl Hamilton",  new List<string>{"Darryl Hamilton"}},
            {"Lee H Hamilton",  new List<string>{"Lee Hamilton"}},
            {"Marvin Hamlisch",  new List<string>{"Marvin Hamlisch"}},
            {"Lionel Hampton",  new List<string>{"Lionel Hampton"}},
            {"Herbie Hancock",  new List<string>{"Herbie Hancock"}},
            {"George Frederick Handel",  new List<string>{"Handel"}},
            {"Tom Hanks",  new List<string>{"Tom Hanks"}},
            {"Donna Hanover",  new List<string>{"Donna Hanover"}},
            {"Marcia Gay Harden",  new List<string>{"Marcia Gay Harden"}},
            {"Tonya Harding",  new List<string>{"Tonya Harding"}},
            {"Tom Harkin",  new List<string>{"Tom Harkin"}},
            {"Sheldon Harnick",  new List<string>{"Sheldon Harnick"}},
            {"Woody Harrelson",  new List<string>{"Woody Harrelson"}},
            {"Pamela Harriman",  new List<string>{"Pamela Harriman"}},
            {"Ed Harris",  new List<string>{"Ed Harris"}},
            {"Emmylou Harris",  new List<string>{"Emmylou Harris"}},
            {"George Harrison",  new List<string>{"George Harrison"}},
            {"Gary Hart",  new List<string>{"Gary Hart"}},
            {"Kitty Carlisle Hart",  new List<string>{"Kitty Carlisle"}},
            {"Moss Hart",  new List<string>{"Moss Hart"}},
            {"Orrin G Hatch",  new List<string>{"Orrin Hatch"}},
            {"Ethan Hawke",  new List<string>{"Ethan Hawke"}},
            {"Stephen W Hawking",  new List<string>{"Stephen Hawking"}},
            {"Tom Hayden",  new List<string>{"Tom Hayden"}},
            {"Franz Joseph Haydn",  new List<string>{"Haydn"}},
            {"Helen Hayes",  new List<string>{"Helen Hayes"}},
            {"Anne Heche",  new List<string>{"Anne Heche"}},
            {"Hugh Hefner",  new List<string>{"Hugh Hefner"}},
            {"Teresa Heinz Kerry",  new List<string>{"Teresa Heinz Kerry"}},
            {"Werner Heisenberg",  new List<string>{"Heisenberg"}},
            {"Joseph Heller",  new List<string>{"Joseph Heller"}},
            {"Lillian Hellman",  new List<string>{"Lillian Hellman"}},
            {"Jesse Helms",  new List<string>{"Jesse Helms"}},
            {"Leona Helmsley",  new List<string>{"Leona Helmsley"}},
            {"Sally Hemings",  new List<string>{"Sally Hemings"}},
            {"Ernest Hemingway",  new List<string>{"Ernest Hemingway"}},
            {"Skitch Henderson",  new List<string>{"Skitch Henderson"}},
            {"Jimi Hendrix",  new List<string>{"Jimi Hendrix"}},
            {"Audrey Hepburn",  new List<string>{"Audrey Hepburn"}},
            {"Katharine Hepburn",  new List<string>{"Katharine Hepburn"}},
            {"Ben Heppner",  new List<string>{"Ben Heppner"}},
            {"Jerry Herman",  new List<string>{"Jerry Herman"}},
            {"Keith Hernandez",  new List<string>{"Keith Hernandez"}},
            {"Seymour M Hersh",  new List<string>{"Seymour Hersh"}},
            {"Werner Herzog",  new List<string>{"Werner Herzog"}},
            {"Don Hewitt",  new List<string>{"Don Hewitt"}},
            {"Tommy Hilfiger",  new List<string>{"Tommy Hilfiger"}},
            {"Anita Hill",  new List<string>{"Anita Hill"}},
            {"Edmund Hillary",  new List<string>{"Edmund Hillary"}},
            {"Paris Hilton",  new List<string>{"Paris Hilton"}},
            {"Gregory Hines",  new List<string>{"Gregory Hines"}},
            {"Judd Hirsch",  new List<string>{"Judd Hirsch"}},
            {"Al Hirschfeld",  new List<string>{"Al Hirschfeld"}},
            {"Alger Hiss",  new List<string>{"Alger Hiss"}},
            {"Alfred Hitchcock",  new List<string>{"Alfred Hitchcock"}},
            {"Christopher Hitchens",  new List<string>{"Christopher Hitchens"}},
            {"Adolf Hitler",  new List<string>{"Adolf Hitler"}},
            {"David Hockney",  new List<string>{"David Hockney"}},
            {"James P Hoffa",  new List<string>{"James Hoffa"}},
            {"Dustin Hoffman",  new List<string>{"Dustin Hoffman"}},
            {"Philip Seymour Hoffman",  new List<string>{"Philip Seymour Hoffman"}},
            {"Billie Holiday",  new List<string>{"Billie Holiday"}},
            {"Ernest F Hollings",  new List<string>{"Ernest Hollings"}},
            {"Winslow Homer",  new List<string>{"Winslow Homer"}},
            {"J Edgar Hoover",  new List<string>{"J Edgar Hoover"}},
            {"Bob Hope",  new List<string>{"Bob Hope"}},
            {"Anthony Hopkins",  new List<string>{"Anthony Hopkins"}},
            {"Dennis Hopper",  new List<string>{"Dennis Hopper"}},
            {"Lena Horne",  new List<string>{"Lena Horne"}},
            {"Marilyn Horne",  new List<string>{"Marilyn Horne"}},
            {"Harry Houdini",  new List<string>{"Houdini"}},
            {"Whitney Houston",  new List<string>{"Whitney Houston"}},
            {"Ron Howard",  new List<string>{"Ron Howard"}},
            {"Steny H Hoyer",  new List<string>{"Steny Hoyer"}},
            {"Mike Huckabee",  new List<string>{"Mike Huckabee"}},
            {"Kate Hudson",  new List<string>{"Kate Hudson"}},
            {"Rock Hudson",  new List<string>{"Rock Hudson"}},
            {"Arianna Huffington",  new List<string>{"Arianna Huffington"}},
            {"Langston Hughes",  new List<string>{"Langston Hughes"}},
            {"Helen Hunt",  new List<string>{"Helen Hunt"}},
            {"Holly Hunter",  new List<string>{"Holly Hunter"}},
            {"Zora Neale Hurston",  new List<string>{"Zora Neale Hurston"}},
            {"William Hurt",  new List<string>{"William Hurt"}},
            {"Saddam Hussein",  new List<string>{"Saddam Hussein"}},
            {"Anjelica Huston",  new List<string>{"Anjelica Huston"}},
            {"John Huston",  new List<string>{"John Huston"}},
            {"Asa Hutchinson",  new List<string>{"Asa Hutchinson"}},
            {"Tim Hutchinson",  new List<string>{"Tim Hutchinson"}},
            {"Kay Bailey Hutchison",  new List<string>{"Kay Bailey Hutchison"}},
            {"Dmitri Hvorostovsky",  new List<string>{"Dmitri Hvorostovsky"}},
            {"Lee A Iacocca",  new List<string>{"Lee Iacocca"}},
            {"Henrik Ibsen",  new List<string>{"Henrik Ibsen"}},
            {"Carl C Icahn",  new List<string>{"Carl Icahn"}},
            {"Gwen Ifill",  new List<string>{"Gwen Ifill"}},
            {"Don Imus",  new List<string>{"Don Imus"}},
            {"James M Inhofe",  new List<string>{"James Inhofe"}},
            {"Daniel K Inouye",  new List<string>{"Daniel Inouye"}},
            {"Eugene Ionesco",  new List<string>{"Eugene Ionesco"}},
            {"Washington Irving",  new List<string>{"Washington Irving"}},
            {"Walter Isaacson",  new List<string>{"Walter Isaacson"}},
            {"Charles Edward Ives",  new List<string>{"Charles Edward Ives"}},
            {"Hugh Jackman",  new List<string>{"Hugh Jackman"}},
            {"Janet Jackson",  new List<string>{"Janet Jackson"}},
            {"Jesse L Jackson",  new List<string>{"Jesse Jackson"}},
            {"Michael Jackson",  new List<string>{"Michael Jackson"}},
            {"Mick Jagger",  new List<string>{"Mick Jagger"}},
            {"Henry James",  new List<string>{"Henry James"}},
            {"LeBron James",  new List<string>{"LeBron James"}},
            {"Kathleen Hall Jamieson",  new List<string>{"Kathleen Hall Jamieson"}},
            {"Jim Jarmusch",  new List<string>{"Jim Jarmusch"}},
            {" Jay-Z",  new List<string>{"Jay-Z"}},
            {"Thomas Jefferson",  new List<string>{"Thomas Jefferson"}},
            {"Peter Jennings",  new List<string>{"Peter Jennings"}},
            {"Derek Jeter",  new List<string>{"Derek Jeter"}},
            {" Jiang Zemin",  new List<string>{"Jiang Zemin"}},
            {"Bobby Jindal",  new List<string>{"Bobby Jindal"}},
            {"Steven P Jobs",  new List<string>{"Steven Jobs","Steve Jobs"}},
            {"Billy Joel",  new List<string>{"Billy Joel"}},
            {"Scarlett Johansson",  new List<string>{"Scarlett Johansson"}},
            {"Elton John",  new List<string>{"Elton John"}},
            {" John Paul II",  new List<string>{"John Paul II"}},
            {"Andrew Johnson",  new List<string>{"Andrew Johnson"}},
            {"Betsey Johnson",  new List<string>{"Betsey Johnson"}},
            {"Don Johnson",  new List<string>{"Don Johnson"}},
            {"Earvin Johnson",  new List<string>{"Earvin Johnson"}},
            {"Lady Bird Johnson",  new List<string>{"Lady Bird Johnson"}},
            {"Lyndon Baines Johnson",  new List<string>{"Lyndon Johnson"}},
            {"Philip Johnson",  new List<string>{"Philip Johnson"}},
            {"Angelina Jolie",  new List<string>{"Angelina Jolie"}},
            {"James Earl Jones",  new List<string>{"James Earl Jones"}},
            {"Norah Jones",  new List<string>{"Norah Jones"}},
            {"Quincy Jones",  new List<string>{"Quincy Jones"}},
            {"Tommy Lee Jones",  new List<string>{"Tommy Lee Jones"}},
            {"Erica Jong",  new List<string>{"Erica Jong"}},
            {"Janis Joplin",  new List<string>{"Janis Joplin"}},
            {"Barbara Jordan",  new List<string>{"Barbara Jordan"}},
            {"Michael Jordan",  new List<string>{"Michael Jordan"}},
            {"Vernon E Jr Jordan",  new List<string>{"Vernon Jordan"}},
            {"James Joyce",  new List<string>{"James Joyce"}},
            {"Jackie Joyner-Kersee",  new List<string>{"Jackie Joyner"}},
            {" Juan Carlos I",  new List<string>{"Juan Carlos"}},
            {"Ashley Judd",  new List<string>{"Ashley Judd"}},
            {"Raul Julia",  new List<string>{"Raul Julia"}},
            {"Carl Gustav Jung",  new List<string>{"Jung"}},
            {"Sebastian Junger",  new List<string>{"Sebastian Junger"}},
            {"Pauline Kael",  new List<string>{"Pauline Kael"}},
            {"Franz Kafka",  new List<string>{"Kafka"}},
            {"Frida Kahlo",  new List<string>{"Frida Kahlo"}},
            {"Garry Kasparov",  new List<string>{"Garry Kasparov"}},
            {"Jeffrey Katzenberg",  new List<string>{"Jeffrey Katzenberg"}},
            {"Elia Kazan",  new List<string>{"Elia Kazan"}},
            {"Diane Keaton",  new List<string>{"Diane Keaton"}},
            {"Garrison Keillor",  new List<string>{"Garrison Keillor"}},
            {"Harvey Keitel",  new List<string>{"Harvey Keitel"}},
            {"Kitty Kelley",  new List<string>{"Kitty Kelley"}},
            {"Gene Kelly",  new List<string>{"Gene Kelly"}},
            {"R Kelly",  new List<string>{"R Kelly"}},
            {"Jack F Kemp",  new List<string>{"Jack Kemp"}},
            {"Anthony M Kennedy",  new List<string>{"Anthony Kennedy"}},
            {"Carolyn Bessette Kennedy",  new List<string>{"Carolyn Kennedy"}},
            {"Edward M Kennedy",  new List<string>{"Edward Kennedy"}},
            {"Ethel Kennedy",  new List<string>{"Ethel Kennedy"}},
            {"John Fitzgerald Kennedy",  new List<string>{"John Kennedy"}},
            {"Robert Francis Kennedy",  new List<string>{"Robert Kennedy"}},
            {"Jerome Kern",  new List<string>{"Jerome Kern"}},
            {"Jack Kerouac",  new List<string>{"Jack Kerouac"}},
            {"Nancy Kerrigan",  new List<string>{"Nancy Kerrigan"}},
            {"John Kerry",  new List<string>{"John Kerry"}},
            {"Jack Kevorkian",  new List<string>{"Jack Kevorkian"}},
            {"Alan Keyes",  new List<string>{"Alan Keyes"}},
            {"Alicia Keys",  new List<string>{"Alicia Keys"}},
            {"Mikhail B Khodorkovsky",  new List<string>{"Mikhail Khodorkovsky"}},
            {"Nikita S Khrushchev",  new List<string>{"Khrushchev"}},
            {"Nicole Kidman",  new List<string>{"Nicole Kidman"}},
            {" Kim Jong Il",  new List<string>{"Kim Jong Il"}},
            {"Jimmy Kimmel",  new List<string>{"Jimmy Kimmel"}},
            {"Angus Jr King",  new List<string>{"Angus King"}},
            {"B B King",  new List<string>{"B B King"}},
            {"Billie Jean King",  new List<string>{"Billie Jean King"}},
            {"Coretta Scott King",  new List<string>{"Coretta Scott King"}},
            {"Larry King",  new List<string>{"Larry King"}},
            {"Rodney Glen King",  new List<string>{"Rodney King"}},
            {"Stephen King",  new List<string>{"Stephen King"}},
            {"Ben Kingsley",  new List<string>{"Ben Kingsley"}},
            {"Michael Kinsley",  new List<string>{"Michael Kinsley"}},
            {"Rudyard Kipling",  new List<string>{"Rudyard Kipling"}},
            {"Henry A Kissinger",  new List<string>{"Henry A Kissinger"}},
            {"Eartha Kitt",  new List<string>{"Eartha Kitt"}},
            {"Gustav Klimt",  new List<string>{"Gustav Klimt"}},
            {"Kevin Kline",  new List<string>{"Kevin Kline"}},
            {"Leon Klinghoffer",  new List<string>{"Leon Klinghoffer"}},
            {"Beyonce Knowles",  new List<string>{"Beyonce Knowles"}},
            {"Edward I Koch",  new List<string>{"Edward Koch"}},
            {"C Everett Koop",  new List<string>{"C Everett Koop"}},
            {"Ted Koppel",  new List<string>{"Ted Koppel"}},
            {"Michael Kors",  new List<string>{"Michael Kors"}},
            {"Sandy Koufax",  new List<string>{"Sandy Koufax"}},
            {"Jon Krakauer",  new List<string>{"Jon Krakauer"}},
            {"Diana Krall",  new List<string>{"Diana Krall"}},
            {"William Kristol",  new List<string>{"William Kristol"}},
            {"Paul Krugman",  new List<string>{"Paul Krugman"}},
            {"Stanley Kubrick",  new List<string>{"Stanley Kubrick"}},
            {"Charles Kuralt",  new List<string>{"Charles Kuralt"}},
            {"Anthony Lake",  new List<string>{"Anthony Lake"}},
            {"Nathan Lane",  new List<string>{"Nathan Lane"}},
            {"K D Lang",  new List<string>{"K D Lang"}},
            {"Jessica Lange",  new List<string>{"Jessica Lange"}},
            {"Frank Langella",  new List<string>{"Frank Langella"}},
            {"Angela Lansbury",  new List<string>{"Angela Lansbury"}},
            {"Wayne LaPierre",  new List<string>{"Wayne LaPierre"}},
            {"Cyndi Lauper",  new List<string>{"Cyndi Lauper"}},
            {"Ralph Lauren",  new List<string>{"Ralph Lauren"}},
            {"Arthur Laurents",  new List<string>{"Arthur Laurents"}},
            {"Frank R Lautenberg",  new List<string>{"Frank Lautenberg"}},
            {"Jude Law",  new List<string>{"Jude Law"}},
            {"Jacob Lawrence",  new List<string>{"Jacob Lawrence"}},
            {"Martin Lawrence",  new List<string>{"Martin Lawrence"}},
            {"John Le Carre",  new List<string>{"John Le Carre"}},
            {"Jean-Marie Le Pen",  new List<string>{"Jean-Marie Le Pen"}},
            {"Patrick J Leahy",  new List<string>{"Patrick Leahy"}},
            {"Norman Lear",  new List<string>{"Norman Lear"}},
            {"Denis Leary",  new List<string>{"Denis Leary"}},
            {"Heath Ledger",  new List<string>{"Heath Ledger"}},
            {"Ang Lee",  new List<string>{"Ang Lee"}},
            {"Peggy Lee",  new List<string>{"Peggy Lee"}},
            {"Robert E Lee",  new List<string>{"Robert E Lee"}},
            {"Spike Lee",  new List<string>{"Spike Lee"}},
            {"Stan Lee",  new List<string>{"Stan Lee"}},
            {"Jim Lehrer",  new List<string>{"Jim Lehrer"}},
            {"Annie Leibovitz",  new List<string>{"Annie Leibovitz"}},
            {"Jennifer Jason Leigh",  new List<string>{"Jennifer Jason Leigh"}},
            {"Mike Leigh",  new List<string>{"Mike Leigh"}},
            {"Vladimir Lenin",  new List<string>{"Lenin"}},
            {"John Lennon",  new List<string>{"John Lennon"}},
            {"Jay Leno",  new List<string>{"Jay Leno"}},
            {"Elmore Leonard",  new List<string>{"Elmore Leonard"}},
            {"Sugar Ray Leonard",  new List<string>{"Sugar Ray Leonard"}},
            {"Alan Jay Lerner",  new List<string>{"Alan Jay Lerner"}},
            {"Warner Leroy",  new List<string>{"Warner Leroy"}},
            {"David Letterman",  new List<string>{"David Letterman"}},
            {"Carl Levin",  new List<string>{"Carl Levin"}},
            {"James Levine",  new List<string>{"James Levine"}},
            {"Barry Levinson",  new List<string>{"Barry Levinson"}},
            {"Harold O Levy",  new List<string>{"Harold Levy"}},
            {"Leon Levy",  new List<string>{"Leon Levy"}},
            {"Monica S Lewinsky",  new List<string>{"Monica Lewinsky"}},
            {"Jerry Lewis",  new List<string>{"Jerry Lewis"}},
            {"Roy Lichtenstein",  new List<string>{"Roy Lichtenstein"}},
            {"Maya Lin",  new List<string>{"Maya Lin"}},
            {"Abraham Lincoln",  new List<string>{"Lincoln"}},
            {"Anne Morrow Lindbergh",  new List<string>{"Anne Lindbergh"}},
            {"Charles A Lindbergh",  new List<string>{"Charles Lindbergh"}},
            {"John V Lindsay",  new List<string>{"John Lindsay"}},
            {"Laura Linney",  new List<string>{"Laura Linney"}},
            {"Tara Lipinski",  new List<string>{"Tara Lipinski"}},
            {"Franz Liszt",  new List<string>{"Franz Liszt"}},
            {"John Lithgow",  new List<string>{"John Lithgow"}},
            {"Andrew Lloyd Webber",  new List<string>{"Andrew Lloyd Webber"}},
            {"Frank Loesser",  new List<string>{"Frank Loesser"}},
            {"Lindsay Lohan",  new List<string>{"Lindsay Lohan"}},
            {"Courtney Love",  new List<string>{"Courtney Love"}},
            {"Lyle Lovett",  new List<string>{"Lyle Lovett"}},
            {"George Lucas",  new List<string>{"George Lucas"}},
            {"David Lynch",  new List<string>{"David Lynch"}},
            {"Yo-Yo Ma",  new List<string>{"Yo-Yo Ma"}},
            {"Shirley MacLaine",  new List<string>{"Shirley MacLaine"}},
            {"William H Macy",  new List<string>{"William Macy"}},
            {"Bernard L Madoff",  new List<string>{"Bernard Madoff"}},
            {"Ira C Magaziner",  new List<string>{"Ira Magaziner"}},
            {"Bill Maher",  new List<string>{"Bill Maher"}},
            {"Gustav Mahler",  new List<string>{"Gustav Mahler"}},
            {"Norman Mailer",  new List<string>{"Norman Mailer"}},
            {" Malcolm X",  new List<string>{"Malcolm X"}},
            {"John Malkovich",  new List<string>{"John Malkovich"}},
            {"Louis Malle",  new List<string>{"Louis Malle"}},
            {"Edouard Manet",  new List<string>{"Manet"}},
            {"Thomas Mann",  new List<string>{"Thomas Mann"}},
            {"Peyton Manning",  new List<string>{"Peyton Manning"}},
            {"Joe Mantegna",  new List<string>{"Joe Mantegna"}},
            {"Mickey Mantle",  new List<string>{"Mickey Mantle"}},
            {" Mao Zedong",  new List<string>{" Mao"}},
            {"Ferdinand E Marcos",  new List<string>{"Ferdinand Marcos"}},
            {"Edward J Markey",  new List<string>{"Edward Markey"}},
            {"Bob Marley",  new List<string>{"Bob Marley"}},
            {"Branford Marsalis",  new List<string>{"Branford Marsalis"}},
            {"Wynton Marsalis",  new List<string>{"Wynton Marsalis"}},
            {"Garry Marshall",  new List<string>{"Garry Marshall"}},
            {"Rob Marshall",  new List<string>{"Rob Marshall"}},
            {"Thurgood Marshall",  new List<string>{"Thurgood Marshall"}},
            {"Dean Martin",  new List<string>{"Dean Martin"}},
            {"Ricky Martin",  new List<string>{"Ricky Martin"}},
            {"Steve Martin",  new List<string>{"Steve Martin"}},
            {"Groucho Marx",  new List<string>{"Groucho Marx"}},
            {"Karl Marx",  new List<string>{"Karl Marx"}},
            {"Jackie Mason",  new List<string>{"Jackie Mason"}},
            {"Jules Massenet",  new List<string>{"Jules Massenet"}},
            {"Marcello Mastroianni",  new List<string>{"Mastroianni"}},
            {"Kurt Masur",  new List<string>{"Kurt Masur"}},
            {"Henri Matisse",  new List<string>{"Matisse"}},
            {"Elaine May",  new List<string>{"Elaine May"}},
            {"Willie Mays",  new List<string>{"Willie Mays"}},
            {"Terry McAuliffe",  new List<string>{"Terry McAuliffe"}},
            {"Cindy McCain",  new List<string>{"Cindy McCain"}},
            {"John McCain",  new List<string>{"John McCain"}},
            {"Joseph R McCarthy",  new List<string>{"Joseph McCarthy"}},
            {"Paul McCartney",  new List<string>{"Paul McCartney"}},
            {"Stanley A McChrystal",  new List<string>{"Stanley McChrystal"}},
            {"Matthew McConaughey",  new List<string>{"Matthew McConaughey"}},
            {"Mitch McConnell",  new List<string>{"Mitch McConnell"}},
            {"David McCullough",  new List<string>{"David McCullough"}},
            {"John McEnroe",  new List<string>{"John McEnroe"}},
            {"George S McGovern",  new List<string>{"George McGovern"}},
            {"Mark McGwire",  new List<string>{"Mark McGwire"}},
            {"Marian Mcpartland",  new List<string>{"Marian Mcpartland"}},
            {"Timothy James Mcveigh",  new List<string>{"Timothy Mcveigh"}},
            {"Zubin Mehta",  new List<string>{"Zubin Mehta"}},
            {"John Mellencamp",  new List<string>{"John Mellencamp"}},
            {"Paul Mellon",  new List<string>{"Paul Mellon"}},
            {"Herman Melville",  new List<string>{"Herman Melville"}},
            {"Felix Mendelssohn",  new List<string>{"Mendelssohn"}},
            {"Sam Mendes",  new List<string>{"Sam Mendes"}},
            {"Robert Menendez",  new List<string>{"Robert Menendez"}},
            {"Alan Menken",  new List<string>{"Alan Menken"}},
            {"Idina Menzel",  new List<string>{"Idina Menzel"}},
            {"Johnny Mercer",  new List<string>{"Johnny Mercer"}},
            {"Ismail Merchant",  new List<string>{"Ismail Merchant"}},
            {"Angela Merkel",  new List<string>{"Angela Merkel"}},
            {"David Merrick",  new List<string>{"David Merrick"}},
            {"Kweisi Mfume",  new List<string>{"Kweisi Mfume"}},
            {"Lorne Michaels",  new List<string>{"Lorne Michaels"}},
            {" Michelangelo Buonarroti",  new List<string>{"Michelangelo"}},
            {"Bette Midler",  new List<string>{"Bette Midler"}},
            {"Barbara A Mikulski",  new List<string>{"Barbara Mikulski"}},
            {"Michael R Milken",  new List<string>{"Michael Milken"}},
            {"Arthur Miller",  new List<string>{"Arthur Miller"}},
            {"Dennis Miller",  new List<string>{"Dennis Miller"}},
            {"Liza Minnelli",  new List<string>{"Liza Minnelli"}},
            {"Joan Miro",  new List<string>{"Joan Miro"}},
            {"Helen Mirren",  new List<string>{"Helen Mirren"}},
            {"Elvis Mitchell",  new List<string>{"Elvis Mitchell"}},
            {"George J Mitchell",  new List<string>{"George Mitchell"}},
            {"Joni Mitchell",  new List<string>{"Joni Mitchell"}},
            {"Margaret Mitchell",  new List<string>{"Margaret Mitchell"}},
            {" Mobutu Sese Seko",  new List<string>{"Mobutu"}},
            {" Moby",  new List<string>{"Moby"}},
            {"Art Modell",  new List<string>{"Art Modell"}},
            {"Amedeo Modigliani",  new List<string>{"Modigliani"}},
            {" Moliere",  new List<string>{"Moliere"}},
            {"Susan Molinari",  new List<string>{"Susan Molinari"}},
            {"Walter F Mondale",  new List<string>{"Walter F Mondale"}},
            {"Claude Monet",  new List<string>{"Monet"}},
            {"Thelonious Monk",  new List<string>{"Thelonious Monk"}},
            {"Marilyn Monroe",  new List<string>{"Marilyn Monroe"}},
            {"Joe Montana",  new List<string>{"Joe Montana"}},
            {"Yves Montand",  new List<string>{"Yves Montand"}},
            {"Sun Myung Moon",  new List<string>{"Sun Myung Moon"}},
            {"Leslie Moonves",  new List<string>{"Leslie Moonves"}},
            {"Demi Moore",  new List<string>{"Demi Moore"}},
            {"Julianne Moore",  new List<string>{"Julianne Moore"}},
            {"Mary Tyler Moore",  new List<string>{"Mary Tyler Moore"}},
            {"Michael Moore",  new List<string>{"Michael Moore"}},
            {"Alanis Morissette",  new List<string>{"Alanis Morissette"}},
            {"Dick Morris",  new List<string>{"Dick Morris"}},
            {"Errol Morris",  new List<string>{"Errol Morris"}},
            {"Toni Morrison",  new List<string>{"Toni Morrison"}},
            {"Robert Moses",  new List<string>{"Robert Moses"}},
            {"Bill Moyers",  new List<string>{"Bill Moyers"}},
            {"Daniel Patrick Moynihan",  new List<string>{"Daniel Moynihan"}},
            {"Wolfgang Amadeus Mozart",  new List<string>{"Mozart"}},
            {"Hosni Mubarak",  new List<string>{"Mubarak"}},
            {"Robert S III Mueller",  new List<string>{"Robert S III Mueller"}},
            {"Robert Mugabe",  new List<string>{"Robert Mugabe"}},
            {"Rupert Murdoch",  new List<string>{"Rupert Murdoch"}},
            {"Eddie Murphy",  new List<string>{"Eddie Murphy"}},
            {"Bill Murray",  new List<string>{"Bill Murray"}},
            {"Patty Murray",  new List<string>{"Patty Murray"}},
            {"John P Murtha",  new List<string>{"John Murtha"}},
            {"Benito Mussolini",  new List<string>{"Mussolini"}},
            {"Vladimir Nabokov",  new List<string>{"Vladimir Nabokov"}},
            {"Rafael Nadal",  new List<string>{"Rafael Nadal"}},
            {"Ralph Nader",  new List<string>{"Ralph Nader"}},
            {"Jerrold Nadler",  new List<string>{"Jerrold Nadler"}},
            {"Joe Namath",  new List<string>{"Joe Namath"}},
            {" Napoleon I",  new List<string>{"Napoleon"}},
            {"Janet Napolitano",  new List<string>{"Janet Napolitano"}},
            {"Martina Navratilova",  new List<string>{"Martina Navratilova"}},
            {"John D Negroponte",  new List<string>{"John Negroponte"}},
            {"Nicholas Negroponte",  new List<string>{"Nicholas Negroponte"}},
            {"Willie Nelson",  new List<string>{"Willie Nelson"}},
            {"Benjamin Netanyahu",  new List<string>{"Benjamin Netanyahu"}},
            {"Bebe Neuwirth",  new List<string>{"Bebe Neuwirth"}},
            {"Paul Newman",  new List<string>{"Paul Newman"}},
            {"Randy Newman",  new List<string>{"Randy Newman"}},
            {"Isaac Newton",  new List<string>{"Isaac Newton"}},
            {"Czar of Russia Nicholas II",  new List<string>{"Nicholas II"}},
            {"Mike Nichols",  new List<string>{"Mike Nichols"}},
            {"Jack Nicholson",  new List<string>{"Jack Nicholson"}},
            {"Don Nickles",  new List<string>{"Don Nickles"}},
            {"Cynthia Nixon",  new List<string>{"Cynthia Nixon"}},
            {"Richard Milhous Nixon",  new List<string>{"Richard Nixon"}},
            {"Nick Nolte",  new List<string>{"Nick Nolte"}},
            {"Peggy Noonan",  new List<string>{"Peggy Noonan"}},
            {"Manuel Antonio Noriega",  new List<string>{"Noriega"}},
            {"Jessye Norman",  new List<string>{"Jessye Norman"}},
            {"Oliver L North",  new List<string>{"Oliver North"}},
            {"Eleanor Holmes Norton",  new List<string>{"Eleanor Holmes Norton"}},
            {"Sam Nunn",  new List<string>{"Sam Nunn"}},
            {"Rudolf Nureyev",  new List<string>{"Rudolf Nureyev"}},
            {"Joyce Carol Oates",  new List<string>{"Joyce Carol Oates"}},
            {"Barack Obama",  new List<string>{"Barack Obama"}},
            {"Michelle Obama",  new List<string>{"Michelle Obama"}},
            {"Keith Olbermann",  new List<string>{"Keith Olbermann"}},
            {"Laurence Olivier",  new List<string>{"Laurence Olivier"}},
            {"Frederick Law Olmsted",  new List<string>{"Frederick Law Olmsted"}},
            {"Theodore B Olson",  new List<string>{"Theodore Olson","Ted Olson"}},
            {"Jacqueline Kennedy Onassis",  new List<string>{"Jacqueline Kennedy","Jackie Kennedy"}},
            {"Yoko Ono",  new List<string>{"Yoko Ono"}},
            {"Jerry Orbach",  new List<string>{"Jerry Orbach"}},
            {"Daniel Ortega Saavedra",  new List<string>{"Daniel Ortega"}},
            {"George Orwell",  new List<string>{"George Orwell"}},
            {"Ozzy Osbourne",  new List<string>{"Ozzy Osbourne"}},
            {"Chris Osgood",  new List<string>{"Chris Osgood"}},
            {"Lee Harvey Oswald",  new List<string>{"Lee Harvey Oswald"}},
            {"Michael Ovitz",  new List<string>{"Michael Ovitz"}},
            {"Al Pacino",  new List<string>{"Al Pacino"}},
            {"George Packer",  new List<string>{"George Packer"}},
            {"Larry Page",  new List<string>{"Larry Page"}},
            {"Sarah Palin",  new List<string>{"Sarah Palin"}},
            {"Gwyneth Paltrow",  new List<string>{"Gwyneth Paltrow"}},
            {"Leon E Panetta",  new List<string>{"Leon E Panetta"}},
            {"Charlie Parker",  new List<string>{"Charlie Parker"}},
            {"Dorothy Parker",  new List<string>{"Dorothy Parker"}},
            {"Mary-Louise Parker",  new List<string>{"Mary-Louise Parker"}},
            {"Sarah Jessica Parker",  new List<string>{"Sarah Jessica Parker"}},
            {"Rosa Parks",  new List<string>{"Rosa Parks"}},
            {"Estelle Parsons",  new List<string>{"Estelle Parsons"}},
            {"Dolly Parton",  new List<string>{"Dolly Parton"}},
            {"George E Pataki",  new List<string>{"George Pataki"}},
            {"Joe Paterno",  new List<string>{"Joe Paterno"}},
            {"Mandy Patinkin",  new List<string>{"Mandy Patinkin"}},
            {"Ron Paul",  new List<string>{"Ron Paul"}},
            {"John Allen Paulos",  new List<string>{"John Allen Paulos"}},
            {"Henry M Jr Paulson",  new List<string>{"Henry Paulson","Hank Paulson"}},
            {"Luciano Pavarotti",  new List<string>{"Luciano Pavarotti"}},
            {"Tim Pawlenty",  new List<string>{"Tim Pawlenty"}},
            {"Walter Payton",  new List<string>{"Walter Payton"}},
            {"Daniel Pearl",  new List<string>{"Daniel Pearl"}},
            {"I M Pei",  new List<string>{"I M Pei"}},
            {" Pele",  new List<string>{"Pele"}},
            {"Nancy Pelosi",  new List<string>{"Nancy Pelosi"}},
            {"Sean Penn",  new List<string>{"Sean Penn"}},
            {"Sonny Perdue",  new List<string>{"Sonny Perdue"}},
            {"Shimon Peres",  new List<string>{"Shimon Peres"}},
            {"Rosie Perez",  new List<string>{"Rosie Perez"}},
            {"Itzhak Perlman",  new List<string>{"Itzhak Perlman"}},
            {"Juan Domingo Peron",  new List<string>{"Juan Peron"}},
            {"Ross Perot",  new List<string>{"Ross Perot"}},
            {"Bernadette Peters",  new List<string>{"Bernadette Peters"}},
            {"Oscar Peterson",  new List<string>{"Oscar Peterson"}},
            {"David H Petraeus",  new List<string>{"David Petraeus"}},
            {"Michelle Pfeiffer",  new List<string>{"Michelle Pfeiffer"}},
            {"Michael Phelps",  new List<string>{"Michael Phelps"}},
            {"Regis Philbin",  new List<string>{"Regis Philbin"}},
            {"Joaquin Phoenix",  new List<string>{"Joaquin Phoenix"}},
            {"Pablo Picasso",  new List<string>{"Picasso"}},
            {"T Boone Jr Pickens",  new List<string>{"T Boone Pickens"}},
            {"David Hyde Pierce",  new List<string>{"David Hyde Pierce"}},
            {"Augusto Pinochet Ugarte",  new List<string>{"Augusto Pinochet"}},
            {"Harold Pinter",  new List<string>{"Harold Pinter"}},
            {"Scottie Pippen",  new List<string>{"Scottie Pippen"}},
            {"Oscar Pistorius",  new List<string>{"Oscar Pistorius"}},
            {"Brad Pitt",  new List<string>{"Brad Pitt"}},
            {"Valerie Plame",  new List<string>{"Valerie Plame"}},
            {"Sylvia Plath",  new List<string>{"Sylvia Plath"}},
            {"George Plimpton",  new List<string>{"George Plimpton"}},
            {"Christopher Plummer",  new List<string>{"Christopher Plummer"}},
            {"John D Podesta",  new List<string>{"John Podesta"}},
            {"Edgar Allan Poe",  new List<string>{"Edgar Allan Poe"}},
            {"John M Poindexter",  new List<string>{"John Poindexter"}},
            {"Sidney Poitier",  new List<string>{"Sidney Poitier"}},
            {" Pol Pot",  new List<string>{"Pol Pot"}},
            {"Roman Polanski",  new List<string>{"Roman Polanski"}},
            {"Michael Pollan",  new List<string>{"Michael Pollan"}},
            {"Jackson Pollock",  new List<string>{"Jackson Pollock"}},
            {"Parker Posey",  new List<string>{"Parker Posey"}},
            {"Francis Poulenc",  new List<string>{"Francis Poulenc"}},
            {"Ezra Pound",  new List<string>{"Ezra Pound"}},
            {"Samantha Power",  new List<string>{"Samantha Power"}},
            {"Elvis Presley",  new List<string>{"Elvis Presley"}},
            {"Andre Previn",  new List<string>{"Andre Previn"}},
            {"Leontyne Price",  new List<string>{"Leontyne Price"}},
            {"Charles O III Prince",  new List<string>{"Prince Charles"}},
            {"Harold Prince",  new List<string>{"Harold Prince"}},
            {"Sergei Prokofiev",  new List<string>{"Prokofiev"}},
            {"Marcel Proust",  new List<string>{"Proust"}},
            {"Richard Pryor",  new List<string>{"Richard Pryor"}},
            {"Giacomo Puccini",  new List<string>{"Giacomo Puccini"}},
            {"Tito Puente",  new List<string>{"Tito Puente"}},
            {"Vladimir V Putin",  new List<string>{"Putin"}},
            {"Muammar el- Qaddafi",  new List<string>{"Qaddafi"}},
            {"Dennis Quaid",  new List<string>{"Dennis Quaid"}},
            {"Dan Quayle",  new List<string>{"Dan Quayle"}},
            {" Queen Latifah",  new List<string>{"Queen Latifah"}},
            {"Anthony Quinn",  new List<string>{"Anthony Quinn"}},
            {"Yitzhak Rabin",  new List<string>{"Yitzhak Rabin"}},
            {"Sergei Rachmaninoff",  new List<string>{"Rachmaninoff"}},
            {"Daniel Radcliffe",  new List<string>{"Daniel Radcliffe"}},
            {"Gilda Radner",  new List<string>{"Gilda Radner"}},
            {"Bonnie Raitt",  new List<string>{"Bonnie Raitt"}},
            {"Samuel Ramey",  new List<string>{"Samuel Ramey"}},
            {"Ayn Rand",  new List<string>{"Ayn Rand"}},
            {"Tony Randall",  new List<string>{"Tony Randall"}},
            {"Dan Rather",  new List<string>{"Dan Rather"}},
            {"James Earl Ray",  new List<string>{"James Earl Ray"}},
            {"Nancy Reagan",  new List<string>{"Nancy Reagan"}},
            {"Ronald Wilson Reagan",  new List<string>{"Ronald Reagan"}},
            {"Robert Redford",  new List<string>{"Robert Redford"}},
            {"Lynn Redgrave",  new List<string>{"Lynn Redgrave"}},
            {"Vanessa Redgrave",  new List<string>{"Vanessa Redgrave"}},
            {"Sumner M Redstone",  new List<string>{"Sumner M Redstone"}},
            {"Christopher Reeve",  new List<string>{"Christopher Reeve"}},
            {"Keanu Reeves",  new List<string>{"Keanu Reeves"}},
            {"William H Rehnquist",  new List<string>{"William Rehnquist"}},
            {"Robert B Reich",  new List<string>{"Robert Reich"}},
            {"Harry Reid",  new List<string>{"Harry Reid"}},
            {"John C Reilly",  new List<string>{"John C Reilly"}},
            {"Carl Reiner",  new List<string>{"Carl Reiner"}},
            {"Rob Reiner",  new List<string>{"Rob Reiner"}},
            {" Rembrandt Harmenszoon van Rijn",  new List<string>{"Rembrandt"}},
            {"David Remnick",  new List<string>{"David Remnick"}},
            {"Edward G Rendell",  new List<string>{"Edward Rendell","Ed Rendell"}},
            {"Janet Reno",  new List<string>{"Janet Reno"}},
            {"Jean Renoir",  new List<string>{"Renoir"}},
            {"Burt Reynolds",  new List<string>{"Burt Reynolds"}},
            {"Debbie Reynolds",  new List<string>{"Debbie Reynolds"}},
            {"Christina Ricci",  new List<string>{"Christina Ricci"}},
            {"Condoleezza Rice",  new List<string>{"Condoleezza Rice"}},
            {"Keith Richards",  new List<string>{"Keith Richards"}},
            {"Bill Richardson",  new List<string>{"Bill Richardson"}},
            {"Natasha Richardson",  new List<string>{"Natasha Richardson"}},
            {"Tom Ridge",  new List<string>{"Tom Ridge"}},
            {"Diana Rigg",  new List<string>{"Diana Rigg"}},
            {"Chita Rivera",  new List<string>{"Chita Rivera"}},
            {"Diego Rivera",  new List<string>{"Diego Rivera"}},
            {"Geraldo Rivera",  new List<string>{"Geraldo Rivera"}},
            {"Joan Rivers",  new List<string>{"Joan Rivers"}},
            {"Alice M Rivlin",  new List<string>{"Alice Rivlin"}},
            {"Phil Rizzuto",  new List<string>{"Phil Rizzuto"}},
            {"Max Roach",  new List<string>{"Max Roach"}},
            {"Jason Robards",  new List<string>{"Jason Robards"}},
            {"Charles S Robb",  new List<string>{"Charles Robb"}},
            {"Jerome Robbins",  new List<string>{"Jerome Robbins"}},
            {"Tim Robbins",  new List<string>{"Tim Robbins"}},
            {"Cokie Roberts",  new List<string>{"Cokie Roberts"}},
            {"John G Jr Roberts",  new List<string>{"John Roberts"}},
            {"Julia Roberts",  new List<string>{"Julia Roberts"}},
            {"Paul Robeson",  new List<string>{"Paul Robeson"}},
            {"Jackie Robinson",  new List<string>{"Jackie Robinson"}},
            {"Chris Rock",  new List<string>{"Chris Rock"}},
            {"David Rockefeller",  new List<string>{"David Rockefeller"}},
            {"Laurance S Rockefeller",  new List<string>{"Laurance Rockefeller"}},
            {"Nelson Aldrich Rockefeller",  new List<string>{"Nelson Rockefeller"}},
            {"Norman Rockwell",  new List<string>{"Norman Rockwell"}},
            {"Sam Rockwell",  new List<string>{"Sam Rockwell"}},
            {"Dennis Rodman",  new List<string>{"Dennis Rodman"}},
            {"Alex Rodriguez",  new List<string>{"Alex Rodriguez"}},
            {"Richard Rogers",  new List<string>{"Richard Rogers"}},
            {"Roy Rogers",  new List<string>{"Roy Rogers"}},
            {"Al Roker",  new List<string>{"Al Roker"}},
            {"Sonny Rollins",  new List<string>{"Sonny Rollins"}},
            {"Mitt Romney",  new List<string>{"Mitt Romney"}},
            {"Linda Ronstadt",  new List<string>{"Linda Ronstadt"}},
            {"Eleanor Roosevelt",  new List<string>{"Eleanor Roosevelt"}},
            {"Franklin Delano Roosevelt",  new List<string>{"Franklin Roosevelt"}},
            {"Theodore Roosevelt",  new List<string>{"Theodore Roosevelt"}},
            {"Charlie Rose",  new List<string>{"Charlie Rose"}},
            {"Pete Rose",  new List<string>{"Pete Rose"}},
            {"Dennis B Ross",  new List<string>{"Dennis Ross"}},
            {"Diana Ross",  new List<string>{"Diana Ross"}},
            {"Wilbur L Jr Ross",  new List<string>{"Wilbur Ross"}},
            {"Isabella Rossellini",  new List<string>{"Isabella Rossellini"}},
            {"Gioacchino Rossini",  new List<string>{"Rossini"}},
            {"Dan Rostenkowski",  new List<string>{"Dan Rostenkowski"}},
            {"Mstislav Rostropovich",  new List<string>{"Mstislav Rostropovich"}},
            {"Philip Roth",  new List<string>{"Philip Roth"}},
            {"Mark Rothko",  new List<string>{"Mark Rothko"}},
            {"Karl Rove",  new List<string>{"Karl Rove"}},
            {"J K Rowling",  new List<string>{"J K Rowling"}},
            {"Peter Paul Rubens",  new List<string>{"Rubens"}},
            {"Paul Rudd",  new List<string>{"Paul Rudd"}},
            {"Julius Rudel",  new List<string>{"Julius Rudel"}},
            {"Donald H Rumsfeld",  new List<string>{"Donald Rumsfeld"}},
            {"Geoffrey Rush",  new List<string>{"Geoffrey Rush"}},
            {"Salman Rushdie",  new List<string>{"Salman Rushdie"}},
            {"Tim Russert",  new List<string>{"Tim Russert"}},
            {"George Herman Ruth",  new List<string>{"George Herman Ruth","Babe Ruth"}},
            {"Meg Ryan",  new List<string>{"Meg Ryan"}},
            {"Winona Ryder",  new List<string>{"Winona Ryder"}},
            {"Eero Saarinen",  new List<string>{"Eero Saarinen"}},
            {"Oliver Sacks",  new List<string>{"Oliver Sacks"}},
            {"Anwar El- Sadat",  new List<string>{"Sadat"}},
            {"William Safire",  new List<string>{"William Safire"}},
            {"Nadja Salerno-Sonnenberg",  new List<string>{"Nadja Salerno-Sonnenberg"}},
            {"J D Salinger",  new List<string>{"J D Salinger"}},
            {"Pierre Salinger",  new List<string>{"Pierre Salinger"}},
            {"Bernard Sanders",  new List<string>{"Bernard Sanders","Bernie Sanders"}},
            {"Deion Sanders",  new List<string>{"Deion Sanders"}},
            {"Adam Sandler",  new List<string>{"Adam Sandler"}},
            {"Mark Sanford",  new List<string>{"Mark Sanford"}},
            {"Rick Santorum",  new List<string>{"Rick Santorum"}},
            {"Susan Sarandon",  new List<string>{"Susan Sarandon"}},
            {"Paul S Sarbanes",  new List<string>{"Paul Sarbanes"}},
            {"John Singer Sargent",  new List<string>{"John Singer Sargent"}},
            {"Diane Sawyer",  new List<string>{"Diane Sawyer"}},
            {"Antonin Scalia",  new List<string>{"Antonin Scalia"}},
            {"Barry Scheck",  new List<string>{"Barry Scheck"}},
            {"James R Schlesinger",  new List<string>{"James R Schlesinger"}},
            {"Arnold Schoenberg",  new List<string>{"Schoenberg"}},
            {"Franz Schubert",  new List<string>{"Schubert"}},
            {"Arnold Schwarzenegger",  new List<string>{"Arnold Schwarzenegger"}},
            {"Martin Scorsese",  new List<string>{"Martin Scorsese"}},
            {"A O Scott",  new List<string>{"A O Scott"}},
            {"George C Scott",  new List<string>{"George C Scott"}},
            {"Brent Scowcroft",  new List<string>{"Brent Scowcroft"}},
            {"Steven Seagal",  new List<string>{"Steven Seagal"}},
            {"Kathleen Sebelius",  new List<string>{"Kathleen Sebelius"}},
            {"David Sedaris",  new List<string>{"David Sedaris"}},
            {"Pete Seeger",  new List<string>{"Pete Seeger"}},
            {"Jerry Seinfeld",  new List<string>{"Jerry Seinfeld"}},
            {"Tom Selleck",  new List<string>{"Tom Selleck"}},
            {"Maurice Sendak",  new List<string>{"Maurice Sendak"}},
            {"William Shakespeare",  new List<string>{"Shakespeare"}},
            {"Tupac Shakur",  new List<string>{"Tupac Shakur"}},
            {"Garry Shandling",  new List<string>{"Garry Shandling"}},
            {"Ravi Shankar",  new List<string>{"Ravi Shankar"}},
            {"Ariel Sharon",  new List<string>{"Ariel Sharon"}},
            {"William Shatner",  new List<string>{"William Shatner"}},
            {"George Bernard Shaw",  new List<string>{"George Bernard Shaw"}},
            {"Wallace Shawn",  new List<string>{"Wallace Shawn"}},
            {"Harry Shearer",  new List<string>{"Harry Shearer"}},
            {"Martin Sheen",  new List<string>{"Martin Sheen"}},
            {"Richard C Shelby",  new List<string>{"Richard Shelby"}},
            {"Matthew Shepard",  new List<string>{"Matthew Shepard"}},
            {"Sam Shepard",  new List<string>{"Sam Shepard"}},
            {"Brooke Shields",  new List<string>{"Brooke Shields"}},
            {"Martin Short",  new List<string>{"Martin Short"}},
            {"Dmitri Shostakovich",  new List<string>{"Dmitri Shostakovich"}},
            {"George P Shultz",  new List<string>{"George Shultz"}},
            {"Bud Shuster",  new List<string>{"Bud Shuster"}},
            {"M Night Shyamalan",  new List<string>{"M Night Shyamalan"}},
            {"Jean Sibelius",  new List<string>{"Sibelius"}},
            {"Beverly Sills",  new List<string>{"Beverly Sills"}},
            {"Ron Silver",  new List<string>{"Ron Silver"}},
            {"Sarah Silverman",  new List<string>{"Sarah Silverman"}},
            {"Russell Simmons",  new List<string>{"Russell Simmons"}},
            {"Carly Simon",  new List<string>{"Carly Simon"}},
            {"Neil Simon",  new List<string>{"Neil Simon"}},
            {"Paul Simon",  new List<string>{"Paul Simon"}},
            {"Alan K Simpson",  new List<string>{"Alan Simpson"}},
            {"Jessica Simpson",  new List<string>{"Jessica Simpson"}},
            {"Nicole Brown Simpson",  new List<string>{"Nicole Brown Simpson"}},
            {"O J Simpson",  new List<string>{"O J Simpson"}},
            {"Frank Sinatra",  new List<string>{"Frank Sinatra"}},
            {"Isaac Bashevis Singer",  new List<string>{"Isaac Bashevis Singer"}},
            {"Gary Sinise",  new List<string>{"Gary Sinise"}},
            {"Christian Slater",  new List<string>{"Christian Slater"}},
            {"Leonard Slatkin",  new List<string>{"Leonard Slatkin"}},
            {"Anna Deavere Smith",  new List<string>{"Anna Deavere Smith"}},
            {"Anna Nicole Smith",  new List<string>{"Anna Nicole Smith"}},
            {"Maggie Smith",  new List<string>{"Maggie Smith"}},
            {"Will Smith",  new List<string>{"Will Smith"}},
            {"Jimmy Smits",  new List<string>{"Jimmy Smits"}},
            {"Steven Soderbergh",  new List<string>{"Steven Soderbergh"}},
            {"Georg Solti",  new List<string>{"Georg Solti"}},
            {"Stephen Sondheim",  new List<string>{"Stephen Sondheim"}},
            {"Susan Sontag",  new List<string>{"Susan Sontag"}},
            {"Theodore C Sorensen",  new List<string>{"Theodore Sorensen"}},
            {"Aaron Sorkin",  new List<string>{"Aaron Sorkin"}},
            {"Andrew Ross Sorkin",  new List<string>{"Andrew Ross Sorkin"}},
            {"George Soros",  new List<string>{"George Soros"}},
            {"Sonia Sotomayor",  new List<string>{"Sonia Sotomayor"}},
            {"David H Souter",  new List<string>{"David Souter"}},
            {"Sissy Spacek",  new List<string>{"Sissy Spacek"}},
            {"Kevin Spacey",  new List<string>{"Kevin Spacey"}},
            {"Kate Spade",  new List<string>{"Kate Spade"}},
            {"Britney Spears",  new List<string>{"Britney Spears"}},
            {"Arlen Specter",  new List<string>{"Arlen Specter"}},
            {"Phil Spector",  new List<string>{"Phil Spector"}},
            {"Steven Spielberg",  new List<string>{"Steven Spielberg"}},
            {"Mark Spitz",  new List<string>{"Mark Spitz"}},
            {"Eliot L Spitzer",  new List<string>{"Eliot Spitzer"}},
            {"Benjamin Spock",  new List<string>{"Benjamin Spock"}},
            {"Jerry Springer",  new List<string>{"Jerry Springer"}},
            {"Bruce Springsteen",  new List<string>{"Bruce Springsteen"}},
            {"Joseph Stalin",  new List<string>{"Stalin"}},
            {"Sylvester Stallone",  new List<string>{"Sylvester Stallone"}},
            {"Jean Stapleton",  new List<string>{"Jean Stapleton"}},
            {"Kenneth W Starr",  new List<string>{"Kenneth Starr","Ken Starr"}},
            {"Ringo Starr",  new List<string>{"Ringo Starr"}},
            {"Mary Steenburgen",  new List<string>{"Mary Steenburgen"}},
            {"Edward Steichen",  new List<string>{"Steichen"}},
            {"Ben Stein",  new List<string>{"Ben Stein"}},
            {"Gertrude Stein",  new List<string>{"Gertrude Stein"}},
            {"John Steinbeck",  new List<string>{"John Steinbeck"}},
            {"Gloria Steinem",  new List<string>{"Gloria Steinem"}},
            {"George Stephanopoulos",  new List<string>{"George Stephanopoulos"}},
            {"Howard Stern",  new List<string>{"Howard Stern"}},
            {"Isaac Stern",  new List<string>{"Isaac Stern"}},
            {"John Paul Stevens",  new List<string>{"John Paul Stevens"}},
            {"Ted Stevens",  new List<string>{"Ted Stevens"}},
            {"James B Stewart",  new List<string>{"James Stewart","Jimmy Stewart"}},
            {"Jon Stewart",  new List<string>{"Jon Stewart"}},
            {"Martha Stewart",  new List<string>{"Martha Stewart"}},
            {"Patrick Stewart",  new List<string>{"Patrick Stewart"}},
            {"Ben Stiller",  new List<string>{"Ben Stiller"}},
            {"Edward Durell Stone",  new List<string>{"Edward Durell Stone"}},
            {"Oliver Stone",  new List<string>{"Oliver Stone"}},
            {"Sharon Stone",  new List<string>{"Sharon Stone"}},
            {"Tom Stoppard",  new List<string>{"Tom Stoppard"}},
            {"Harriet Beecher Stowe",  new List<string>{"Harriet Beecher Stowe"}},
            {"Richard Strauss",  new List<string>{"Richard Strauss"}},
            {"Igor Stravinsky",  new List<string>{"Stravinsky"}},
            {"Darryl Strawberry",  new List<string>{"Darryl Strawberry"}},
            {"Meryl Streep",  new List<string>{"Meryl Streep"}},
            {"Barbra Streisand",  new List<string>{"Barbra Streisand"}},
            {"August Strindberg",  new List<string>{"August Strindberg"}},
            {"Elaine Stritch",  new List<string>{"Elaine Stritch"}},
            {"Jule Styne",  new List<string>{"Jule Styne"}},
            {"William Styron",  new List<string>{"William Styron"}},
            {" Suharto",  new List<string>{" Suharto"}},
            {"Andrew Sullivan",  new List<string>{"Andrew Sullivan"}},
            {"Arthur S Sullivan",  new List<string>{"Arthur Sullivan"}},
            {"Arthur Ochs Sulzberger",  new List<string>{"Arthur Sulzberger"}},
            {"Pat Summerall",  new List<string>{"Pat Summerall"}},
            {"Lawrence H Summers",  new List<string>{"Lawrence Summers","Larry Summers"}},
            {"John E Sununu",  new List<string>{"John Sununu"}},
            {"Donald Sutherland",  new List<string>{"Donald Sutherland"}},
            {"Joan Sutherland",  new List<string>{"Joan Sutherland"}},
            {"Kiefer Sutherland",  new List<string>{"Kiefer Sutherland"}},
            {"Hilary Swank",  new List<string>{"Hilary Swank"}},
            {"Tilda Swinton",  new List<string>{"Tilda Swinton"}},
            {"Amy Tan",  new List<string>{"Amy Tan"}},
            {"Jessica Tandy",  new List<string>{"Jessica Tandy"}},
            {"Quentin Tarantino",  new List<string>{"Quentin Tarantino"}},
            {"Elizabeth Taylor",  new List<string>{"Elizabeth Taylor"}},
            {"James Taylor",  new List<string>{"James Taylor"}},
            {"Julie Taymor",  new List<string>{"Julie Taymor"}},
            {"Peter Ilyich Tchaikovsky",  new List<string>{"Tchaikovsky"}},
            {"Edward Teller",  new List<string>{"Edward Teller"}},
            {"George J Tenet",  new List<string>{"George Tenet"}},
            {" Teresa (Mother)",  new List<string>{"Mother Teresa"}},
            {"Bryn Terfel",  new List<string>{"Bryn Terfel"}},
            {"Studs Terkel",  new List<string>{"Studs Terkel"}},
            {"Twyla Tharp",  new List<string>{"Twyla Tharp"}},
            {"Margaret H Thatcher",  new List<string>{"Margaret Thatcher"}},
            {"Charlize Theron",  new List<string>{"Charlize Theron"}},
            {"Clarence Thomas",  new List<string>{"Clarence Thomas"}},
            {"Michael Tilson Thomas",  new List<string>{"Michael Tilson Thomas"}},
            {"Emma Thompson",  new List<string>{"Emma Thompson"}},
            {"Hunter S Thompson",  new List<string>{"Hunter S Thompson"}},
            {"Virgil Thomson",  new List<string>{"Virgil Thomson"}},
            {"Henry David Thoreau",  new List<string>{"Henry David Thoreau"}},
            {"Billy Bob Thornton",  new List<string>{"Billy Bob Thornton"}},
            {"James Thurber",  new List<string>{"Thurber"}},
            {"Uma Thurman",  new List<string>{"Uma Thurman"}},
            {"Strom Thurmond",  new List<string>{"Strom Thurmond"}},
            {"Justin Timberlake",  new List<string>{"Justin Timberlake"}},
            {"J R R Tolkien",  new List<string>{"Tolkien"}},
            {"Leo Tolstoy",  new List<string>{"Leo Tolstoy"}},
            {"Marisa Tomei",  new List<string>{"Marisa Tomei"}},
            {"Lily Tomlin",  new List<string>{"Lily Tomlin"}},
            {"Rip Torn",  new List<string>{"Rip Torn"}},
            {"Kathleen Kennedy Townsend",  new List<string>{"Kathleen Kennedy"}},
            {"Pete Townshend",  new List<string>{"Pete Townshend"}},
            {"John Travolta",  new List<string>{"John Travolta"}},
            {"Laurence H Tribe",  new List<string>{"Laurence H Tribe"}},
            {"Linda R Tripp",  new List<string>{"Linda Tripp"}},
            {"Garry Trudeau",  new List<string>{"Garry Trudeau"}},
            {"Harry S Truman",  new List<string>{"Truman"}},
            {"Donald J Trump",  new List<string>{"Donald Trump"}},
            {"Stanley Tucci",  new List<string>{"Stanley Tucci"}},
            {"Kathleen Turner",  new List<string>{"Kathleen Turner"}},
            {"Ted Turner",  new List<string>{"Ted Turner"}},
            {"Tina Turner",  new List<string>{"Tina Turner"}},
            {"Scott Turow",  new List<string>{"Scott Turow"}},
            {"Shania Twain",  new List<string>{"Shania Twain"}},
            {"Mike Tyson",  new List<string>{"Mike Tyson"}},
            {"Leslie Uggams",  new List<string>{"Leslie Uggams"}},
            {"John Updike",  new List<string>{"John Updike"}},
            {"Dawn Upshaw",  new List<string>{"Dawn Upshaw"}},
            {"Jack Valenti",  new List<string>{"Jack Valenti"}},
            {"Vincent van Gogh",  new List<string>{"Vincent van Gogh"}},
            {"Gus van Sant",  new List<string>{"Gus van Sant"}},
            {"Suzanne Vega",  new List<string>{"Suzanne Vega"}},
            {"Jesse Ventura",  new List<string>{"Jesse Ventura"}},
            {"Giuseppe Verdi",  new List<string>{"Giuseppe Verdi"}},
            {"Gwen Verdon",  new List<string>{"Gwen Verdon"}},
            {"Jan Vermeer",  new List<string>{"Vermeer"}},
            {"Gianni Versace",  new List<string>{"Gianni Versace"}},
            {"Gore Vidal",  new List<string>{"Gore Vidal"}},
            {"Antonio Vivaldi",  new List<string>{"Vivaldi"}},
            {"Jon Voight",  new List<string>{"Jon Voight"}},
            {"Deborah Voigt",  new List<string>{"Deborah Voigt"}},
            {"Anne Sofie von Otter",  new List<string>{"Anne Sofie von Otter"}},
            {"Kurt Vonnegut",  new List<string>{"Kurt Vonnegut"}},
            {"Richard Wagner",  new List<string>{"Richard Wagner"}},
            {"Rufus Wainwright",  new List<string>{"Rufus Wainwright"}},
            {"Kurt Waldheim",  new List<string>{"Kurt Waldheim"}},
            {"Christopher Walken",  new List<string>{"Christopher Walken"}},
            {"Alice Walker",  new List<string>{"Alice Walker"}},
            {"David Foster Wallace",  new List<string>{"David Foster Wallace"}},
            {"George C Wallace",  new List<string>{"George Wallace"}},
            {"Eli Wallach",  new List<string>{"Eli Wallach"}},
            {"Barbara Walters",  new List<string>{"Barbara Walters"}},
            {"Andy Warhol",  new List<string>{"Andy Warhol"}},
            {"Mark R Warner",  new List<string>{"Mark Warner"}},
            {"Denzel Washington",  new List<string>{"Denzel Washington"}},
            {"Maxine Waters",  new List<string>{"Maxine Waters"}},
            {"Evelyn Waugh",  new List<string>{"Evelyn Waugh"}},
            {"Henry A Waxman",  new List<string>{"Henry Waxman"}},
            {"Sigourney Weaver",  new List<string>{"Sigourney Weaver"}},
            {"James H Jr Webb",  new List<string>{"James Webb"}},
            {"William H Webster",  new List<string>{"William Webster"}},
            {"Kurt Weill",  new List<string>{"Kurt Weill"}},
            {"Caspar W Weinberger",  new List<string>{"Caspar Weinberger"}},
            {"Anthony D Weiner",  new List<string>{"Anthony Weiner"}},
            {"Harvey Weinstein",  new List<string>{"Harvey Weinstein"}},
            {"William F Weld",  new List<string>{"William Weld"}},
            {"Cornel West",  new List<string>{"Cornel West"}},
            {"Kanye West",  new List<string>{"Kanye West"}},
            {"Mae West",  new List<string>{"Mae West"}},
            {"Ruth Westheimer",  new List<string>{"Ruth Westheimer"}},
            {"Stanford White",  new List<string>{"Stanford White"}},
            {"Christine Todd Whitman",  new List<string>{"Christine Todd Whitman"}},
            {"Walt Whitman",  new List<string>{"Walt Whitman"}},
            {"Elie Wiesel",  new List<string>{"Elie Wiesel"}},
            {"Dianne Wiest",  new List<string>{"Dianne Wiest"}},
            {"Oscar Wilde",  new List<string>{"Oscar Wilde"}},
            {"Billy Wilder",  new List<string>{"Billy Wilder"}},
            {"Thornton Wilder",  new List<string>{"Thornton Wilder"}},
            {"George F Will",  new List<string>{"George Will"}},
            {"John Williams",  new List<string>{"John Williams"}},
            {"Robin Williams",  new List<string>{"Robin Williams"}},
            {"Serena Williams",  new List<string>{"Serena Williams"}},
            {"Ted Williams",  new List<string>{"Ted Williams"}},
            {"Tennessee Williams",  new List<string>{"Tennessee Williams"}},
            {"Venus Williams",  new List<string>{"Venus Williams"}},
            {"Bruce Willis",  new List<string>{"Bruce Willis"}},
            {"August Wilson",  new List<string>{"August Wilson"}},
            {"Woodrow Wilson",  new List<string>{"Woodrow Wilson"}},
            {"Amy Winehouse",  new List<string>{"Amy Winehouse"}},
            {"Oprah Winfrey",  new List<string>{"Oprah Winfrey"}},
            {"Kate Winslet",  new List<string>{"Kate Winslet"}},
            {"Anna Wintour",  new List<string>{"Anna Wintour"}},
            {"Tom Wolfe",  new List<string>{"Tom Wolfe"}},
            {"Paul D Wolfowitz",  new List<string>{"Paul Wolfowitz"}},
            {"Stevie Wonder",  new List<string>{"Stevie Wonder"}},
            {"Tiger Woods",  new List<string>{"Tiger Woods"}},
            {"Bob Woodward",  new List<string>{"Bob Woodward"}},
            {"Joanne Woodward",  new List<string>{"Joanne Woodward"}},
            {"Virginia Woolf",  new List<string>{"Virginia Woolf"}},
            {"Frank Lloyd Wright",  new List<string>{"Frank Lloyd Wright"}},
            {"Andrew Wyeth",  new List<string>{"Andrew Wyeth"}},
            {"William Butler Yeats",  new List<string>{"William Butler Yeats"}},
            {"Francesca Zambello",  new List<string>{"Francesca Zambello"}},
            {"Frank Zappa",  new List<string>{"Frank Zappa"}},
            {"Renee Zellweger",  new List<string>{"Renee Zellweger"}},
            {"Catherine Zeta-Jones",  new List<string>{"Catherine Zeta-Jones"}},
            {" Zhao Ziyang",  new List<string>{"Zhao Ziyang"}},
            {"Pinchas Zukerman",  new List<string>{"Pinchas Zukerman"}}
        };

        public override async Task<Birthday> CreateOrUpdateAsync(Birthday birthday)
        {
            List<string> categoryStrings = new List<string>();
            birthday.Categories.ForEach(c => {
                categoryStrings.Add(c.CategoryName);
            });
            ElasticBirthday elasticBirthday = new ElasticBirthday
            {
                dob = birthday.Dob,
                fname = birthday.Fname,
                Id = birthday.Id,
                lname = birthday.Lname,
                sign = birthday.Sign,
                isAlive = birthday.IsAlive,
                categories = categoryStrings.ToArray<string>()
            };
            await elastic.UpdateAsync<ElasticBirthday>(new DocumentPath<ElasticBirthday>(elasticBirthday.Id), u =>
                u.Index("birthdays").Doc(elasticBirthday)
            );
            if (categoryStrings.Count == 0)
            {
                await elastic.UpdateAsync<ElasticBirthday>(new DocumentPath<ElasticBirthday>(elasticBirthday.Id), u =>
                    u.Script(s => s.Source("ctx._source.remove('categories')"))
                );
            }
            return birthday;
        }
        public override async Task<IPage<Birthday>> GetPageAsync(IPageable pageable)
        {
            return await GetPageFilteredAsync(pageable, "");
        }

        public override async Task<IPage<Birthday>> GetPageFilteredAsync(IPageable pageable, string queryJson)
        {
            if (!queryJson.StartsWith("{"))
            {
                // backwards compatibility
                Dictionary<string, object> queryObject = new Dictionary<string, object>();
                queryObject["query"] = queryJson;
                queryObject["view"] = null;
                queryJson = JsonConvert.SerializeObject(queryObject);
            }
            var birthdayRequest = JsonConvert.DeserializeObject<Dictionary<string, object>>(queryJson);
            View<Birthday> view = new View<Birthday>();
            string query = birthdayRequest.ContainsKey("query") ? (string)birthdayRequest["query"] : "";
            if (birthdayRequest.ContainsKey("view") && birthdayRequest["view"] != null)
            {
                view = JsonConvert.DeserializeObject<View<Birthday>>(birthdayRequest["view"].ToString());
            }
            string categoryClause = "";
            if (birthdayRequest.ContainsKey("category") && view.field != null)
            {
                if (view.categoryQuery != null)
                {
                    categoryClause = view.categoryQuery.Replace("{}", (string)birthdayRequest["category"]);
                }
                else if (!birthdayRequest.ContainsKey("focusType"))
                {
                    categoryClause = (string)birthdayRequest["category"] == "-" ? "-" + view.field + ":*" : view.field + ":\"" + birthdayRequest["category"] + "\"";
                }
                if (view.topLevelView != null)
                {
                    View<Birthday> topLevelView = view.topLevelView;
                    string otherCategoryClause = "";
                    if (view.topLevelCategory != null && topLevelView.field != null)
                    {
                        if (birthdayRequest.ContainsKey("focusType"))
                        {
                            string focusType = (string)birthdayRequest["focusType"];
                            if (focusType == "FOCUS")
                            {
                                otherCategoryClause = "_id:" + birthdayRequest["focusId"];
                            }
                            else
                            {
                                otherCategoryClause = "*:columbus"; // obviously, a partial implementation of this
                            }
                        }
                        else if (topLevelView.categoryQuery != null)
                        {
                            otherCategoryClause = view.categoryQuery.Replace("{}", view.topLevelCategory);
                        }
                        else
                        {
                            otherCategoryClause = view.topLevelCategory == "-" ? "-" + topLevelView.field + ":*" : topLevelView.field + ":\"" + view.topLevelCategory + "\"";
                        }
                        categoryClause = otherCategoryClause + (categoryClause.Length > 1 ? " AND (" + categoryClause + ")" : "");
                    }
                }
                if (categoryClause != ""){
                    query = categoryClause + (query.Length > 1 ? " AND (" + query + ")" : "");
                }
            }
            ISearchResponse<ElasticBirthday> searchResponse = null;
            if (query == "" || query == "()")
            {
                searchResponse = await elastic.SearchAsync<ElasticBirthday>(s => s
                    .Size(10000)
                    .Query(q => q
                        .MatchAll()
                    )
                );
            }
            else if (birthdayRequest.ContainsKey("queryRuleset"))
            {
                RulesetOrRule queryRuleset = JsonConvert.DeserializeObject<RulesetOrRule>((string)birthdayRequest["queryRuleset"]);
                JObject obj = (JObject) await RulesetToElasticSearch(queryRuleset);
                if (categoryClause != ""){
                    JObject categoryObject = JObject.Parse("{\"bool\":{\"must\":[{\"query_string\":{\"query\":\"\"}}]}}");
                    categoryObject["bool"]["must"][0]["query_string"]["query"] = categoryClause;
                    ((JArray)categoryObject["bool"]["must"]).Add(obj);
                    obj = categoryObject;
                }
                searchResponse = await elastic.SearchAsync<ElasticBirthday>(s => s
                    .Index("birthdays")
                    .Size(10000)
                    .Query(q => q
                        .Raw(obj.ToString())
                    )
                );
            } else
            {
                searchResponse = await elastic.SearchAsync<ElasticBirthday>(x => x
                    .Index("birthdays")
                    .QueryOnQueryString(query)
                    .Size(10000)
                );
            }
            List<Birthday> content = new List<Birthday>();
            Console.WriteLine(searchResponse.Hits.Count + " hits");
            foreach (var hit in searchResponse.Hits)
            {
                List<Category> listCategory = new List<Category>();
                if (hit.Source.categories != null)
                {
                    hit.Source.categories.ToList().ForEach(c => {
                        listCategory.Add(new Category
                        {
                            CategoryName = c
                        });
                    });
                }
                content.Add(new Birthday
                {
                    Id = hit.Id,
                    Lname = hit.Source.lname,
                    Fname = hit.Source.fname,
                    Dob = hit.Source.dob,
                    Sign = hit.Source.sign,
                    IsAlive = hit.Source.isAlive,
                    Categories = listCategory,
                    Text = Regex.Replace(hit.Source.wikipedia, "<.*?>|&.*?;", string.Empty)
                });
            }
            content = content.OrderBy(b => b.Dob).ToList();
            return new Page<Birthday>(content, pageable, content.Count);
        }

        public async Task<List<string>> GetUniqueFieldValuesAsync(string field)
        {
            var result = await elastic.SearchAsync<Aggregation>(q => q
                .Size(0).Index("birthdays").Aggregations(agg => agg.Terms(
                    "distinct", e =>
                        e.Field(field).Size(10000)
                    )
                )
            );
            List<string> ret = new List<string>();
            ((BucketAggregate)result.Aggregations.ToList()[0].Value).Items.ToList().ForEach(it =>
            {
                KeyedBucket<Object> kb = (KeyedBucket<Object>)it;
                ret.Add(kb.KeyAsString != null ? kb.KeyAsString : (string)kb.Key);
            });
            return ret;
        }

        private async Task<Object> RulesetToElasticSearch(RulesetOrRule rr)
        {
            // this routine converts rulesets into elasticsearch DSL as json.  For inexact matching (contains), it uses the field.  For exact matching (=),
            // it uses the keyworkd fields.  Since those are case sensitive, it forces a search for all cased values that would match insenitively
            if (rr.rules == null)
            {
                JObject ret = new JObject{{
                    "term", new JObject{{
                        "BOGUSFIELD", "CANTMATCH"
                    }}
                }};
                if (rr.@operator.Contains("contains"))
                {
                    string stringValue = (string)rr.value;
                    if (stringValue.StartsWith("/") && (stringValue.EndsWith("/") || stringValue.EndsWith("/i")))
                    {
                        Boolean bCaseInsensitive = stringValue.EndsWith("/i");
                        string re = rr.value.ToString().Substring(1, rr.value.ToString().Length - (bCaseInsensitive ? 3 : 2));
                        string regex = ToElasticRegEx(re.Replace(@"\\",@"\"), bCaseInsensitive);
                        if (regex.StartsWith("^"))
                        {
                            regex = regex.Substring(1, regex.Length - 1);
                        }
                        else
                        {
                            regex = ".*" + regex;
                        }
                        if (regex.EndsWith("$"))
                        {
                            regex = regex.Substring(0, regex.Length - 1);
                        }
                        else
                        {
                            regex += ".*";
                        }
                        if (rr.field == "document")
                        {
                            List<JObject> lstRegexes = "wikipedia,fname,lname,categories,sign,id".Split(',').ToList().Select(s =>
                            {
                                return new JObject{{
                                    "regexp", new JObject{{
                                        s + ".keyword", new JObject{
                                            { "value", regex}
                                            ,{ "flags", "ALL" }
                                            ,{ "rewrite", "constant_score" }
                                        }
                                    }}
                                }};
                            }).ToList();
                            return new JObject{{
                                "bool", new JObject{{
                                    "should", JArray.FromObject(lstRegexes)
                                }}
                            }};
                        }
                        return new JObject{{
                            "regexp", new JObject{{
                                rr.field + ".keyword", new JObject{
                                    { "value", regex}
                                    ,{ "flags", "ALL" }
                                    ,{ "rewrite", "constant_score" }
                                }
                            }}
                        }};
                    }
                    string quote = Regex.IsMatch(rr.value.ToString(), @"\W") ? @"""" : "";
                    ret = new JObject{{
                        "query_string", new JObject{{
                            "query", (rr.field != "document" ? (rr.field + ":") : "") + quote + ((string)rr.value).ToLower().Replace(@"""", @"\""") + quote
                        }}
                    }};
                }
                else if (rr.@operator.Contains("="))
                {
                    List<string> uniqueValues = await GetUniqueFieldValuesAsync(rr.field + ".keyword");
                    List<JObject> oredTerms = uniqueValues.Where(v => v.ToLower() == rr.value.ToString().ToLower()).Select(s =>
                    {
                        return new JObject{{
                            "term", new JObject{{
                                rr.field + ".keyword", s
                            }}
                        }};
                    }).ToList();
                    if (oredTerms.Count > 1)
                    {
                        ret = new JObject{{
                            "bool", new JObject{{
                                "should", JArray.FromObject(oredTerms)
                            }}
                        }};
                    }
                    else if (oredTerms.Count == 1)
                    {
                        ret = oredTerms[0];
                    }
                } else if (rr.@operator.Contains("in")) {
                    List<string> uniqueValues = await GetUniqueFieldValuesAsync(rr.field + ".keyword");
                    // The following creates a list of case sensitive possibilities for the case sensitive 'term' query from case insensitive terms
                    List<string> caseSensitiveMatches = ((JArray)rr.value).Select(v =>
                    {
                        return uniqueValues.Where(s => s.ToLower() == v.ToString().ToLower());
                    }).Aggregate((agg,list) => {
                        return agg.Concat(list).ToList();
                    }).ToList();
                    return new JObject{{
                        "terms", new JObject{{
                            rr.field + ".keyword", JArray.FromObject(caseSensitiveMatches)
                        }}
                    }};
                } else if (rr.@operator.Contains("exists")) {
                    List<JObject> lstExists = new List<JObject>();
                    List<JObject> lstEmptyString = new List<JObject>();
                    lstEmptyString.Add(new JObject{{
                        "term", new JObject{{
                            rr.field + ".keyword",""
                        }}
                    }});
                    lstExists.Add(new JObject{{
                        "exists", new JObject{{
                            "field", rr.field
                        }}
                    }});
                    lstExists.Add(new JObject{{
                        "bool", new JObject{{
                            "must_not", JArray.FromObject(lstEmptyString)
                        }}
                    }});
                    ret = new JObject{{
                        "bool", new JObject{{
                            "must", JArray.FromObject(lstExists)
                        }}
                    }};
                }
                if (rr.@operator.Contains("!") || (rr.@operator == "exists" && !(rr.value != null && (Boolean)rr.value))){
                    ret = new JObject {{
                        "bool", new JObject{{
                            "must_not", JObject.FromObject(ret)
                        }}
                    }};
                }
                return ret;
            }
            else
            {
                List<Object> rls = new List<Object>();
                for (int i = 0; i < rr.rules.Count; i++)
                {
                    rls.Add(await RulesetToElasticSearch(rr.rules[i]));
                }
                if (rr.condition == "and")
                {
                    return new JObject{{
                        "bool", new JObject{{
                            rr.not == true ? "must_not" : "must", JArray.FromObject(rls)
                        }}
                    }};
                }
                Object ret = new JObject{{
                    "bool", new JObject{{
                        "should", JArray.FromObject(rls)
                    }}
                }};
                if (rr.not == true)
                {
                    ret = new JObject{{
                        "bool", new JObject{{
                            "must_not", JObject.FromObject(ret)
                        }}
                    }};
                }
                return ret;
            }
        }
        private string ToElasticRegEx(string pattern, Boolean bCaseInsensitive)
        {
            string ret = "";
            string[] regexTokens = Regex.Split(pattern, @"([\[\]]|\\\\|\\\[|\\\]|\\s|\\n|\\w|\\t|\\d|\\D|.)");
            bool bBracketed = false;
            for (int i = 1; i < regexTokens.Length; i++){
                if (bBracketed){
                    switch (regexTokens[i]){
                        case "]":
                            bBracketed = false;
                            ret += regexTokens[i];
                            break;
                        case @"\s":
                            ret += " \n\t\r";
                            break;
                        case @"\d":
                            ret += "0-9";
                            break;
                        case @"\w":
                            ret += "A-Za-z_";
                            break;
                        case @"\n":
                            ret += "\n";
                            break;
                        case @"\t":
                            ret += "\t";
                            break;
                        default:
                            if (bCaseInsensitive && Regex.IsMatch(regexTokens[i], @"^[A-Za-z]+$")){
                                if ((i + 2) < regexTokens.Length && regexTokens[i + 1] == "-" && Regex.IsMatch(regexTokens[i + 2], @"^[A-Za-z]+$")){
                                    // alpha rannge
                                    ret += (regexTokens[i].ToLower() + "-" + regexTokens[i + 2].ToLower() + regexTokens[i].ToUpper() + "-" + regexTokens[i + 2].ToUpper());
                                    i += 2;
                                } else {
                                    ret += (regexTokens[i].ToLower() + regexTokens[i].ToUpper());
                                }
                            } else {
                                ret += regexTokens[i];
                            }
                            break;
                    }
                } else if (regexTokens[i] == "["){
                    bBracketed = true;
                    ret += regexTokens[i];
                } else if (regexTokens[i] == @"\s"){
                    ret += (@"[ \n\t\r]");
                } else if (regexTokens[i] == @"\d"){
                    ret += (@"[0-9]");
                } else if (regexTokens[i] == @"\w"){
                    ret += (@"[A-Za-z_]");
                } else if (bCaseInsensitive && Regex.IsMatch(regexTokens[i], @"[A-Za-z]")){
                    ret += ("[" + regexTokens[i].ToLower() + regexTokens[i].ToUpper() + "]");
                } else {
                    ret += regexTokens[i];
                }
            }
            return ret;
        }

        private class ElasticBirthday
        {
            public string Id { get; set; }
            public string lname { get; set; }
            public string fname { get; set; }
            public DateTime dob { get; set; }
            public string sign { get; set; }
            public bool isAlive { get; set; }
            public string[] categories {get; set; }
            public string wikipedia {get; set; }
        }
        public async override Task<Birthday> GetOneAsync(object id)
        {
            return await GetOneAsync(id, false);
        }
        public async Task<Birthday> GetOneAsync(object id, bool bText)
        {
            var hit = await elastic.GetAsync<ElasticBirthday>((string)id);
            Birthday birthday = new Birthday
            {
                Id = hit.Id,
                Lname = hit.Source.lname,
                Fname = hit.Source.fname,
                Dob = hit.Source.dob,
                Sign = hit.Source.sign,
                IsAlive = hit.Source.isAlive,
                Categories = new List<Category>()
            };
            if (bText)
            {
                birthday.Text = hit.Source.wikipedia;
            }
            if (hit.Source.categories != null)
            {
                hit.Source.categories.ToList().ForEach(c => {
                    Category category = new Category
                    {
                        CategoryName = c
                    };
                    birthday.Categories.Add(category);
                });
            }
            return birthday;
        }
        public async Task<string> GetOneTextAsync(object id)
        {
            var hit = await elastic.GetAsync<ElasticBirthday>((string)id);
            return hit.Source.wikipedia;
        }

        public async Task<List<Birthday>> GetReferencesFromAsync(string id)
        {
            return null;
        }

        public async Task<List<Birthday>> GetReferencesToAsync(string id)
        {
            List<Birthday> birthdays = new List<Birthday>();
            Birthday bday = await GetOneAsync(id);
            string key = bday.Fname + " " + bday.Lname;
            string query = "\"" + bday.Fname + " " + bday.Lname + "\"~4";
            if (refKeys.ContainsKey(key))
            {
                query = "\"" + String.Join("\"~4 OR \"", refKeys[key]) + "\"~4";
            }
            var searchResponse = await elastic.SearchAsync<ElasticBirthday>(x => x
                .Index("birthdays")
                .QueryOnQueryString(query)
                .Size(10000)
            );
            foreach (var hit in searchResponse.Hits)
            {
                if (hit.Id != id)
                {
                    List<Category> listCategory = new List<Category>();
                    if (hit.Source.categories != null)
                    {
                        hit.Source.categories.ToList().ForEach(c => {
                            listCategory.Add(new Category
                            {
                                CategoryName = c
                            });
                        });
                    }
                    birthdays.Add(new Birthday
                    {
                        Id = hit.Id,
                        Lname = hit.Source.lname,
                        Fname = hit.Source.fname,
                        Dob = hit.Source.dob,
                        Sign = hit.Source.sign,
                        IsAlive = hit.Source.isAlive,
                        Categories = listCategory
                    });
                }
            }
            return birthdays;
        }
    }
}

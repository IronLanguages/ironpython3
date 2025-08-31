# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
-------------------------------------------------------------------------------
INTRODUCTION
Single sentence describing the intent and purpose of the test plan. For example, 
"This test plan addresses the test coverage for the XXX release of the BAR area 
of feature Foo".

EDITORS NOTES
Throughout this document you will find references to documentation in
other packages similar to "See documentation for interop.net.field".  This simply
means that relative to this package (i.e., interop.net), you should follow
the 'field (package)' link at the bottom of this page.

-------------------------------------------------------------------------------
FEATURE GOALS
Mission statement and goal of specific feature team.
This section is used to set the stage for testing's plans and goals in relation
to the feature team and project's goals.

-------------------------------------------------------------------------------
PRIMARY TESTING CONCERN
A statement of what the main critical concerns of the test plan are. An 
itemized list, or short paragraph will suffice.

-------------------------------------------------------------------------------
PRIMARY TESTING FOCUS
A short statement of what items testing will focus on. The testing concerns 
above state what testing is worried about. Focus indicates more of a 
methodology - a statement of how those concerns will be addressed via focus.

-------------------------------------------------------------------------------
REFERENCES
* document name                     location
* test plan                         test plan location
* project specifications            project spec location
* feature specification             feature spec location
* development docs on feature       dev doc location
* bug database queries              location for raid queries
* test case database queries        location for test case queries
* schedule documents                location for schedule documents
* build release server              location of build releases
* source file tree                  location of source file tree
* other related documents           other locations

-------------------------------------------------------------------------------
PERSONNEL
Program Manager:    name and email
Developer:          name and email
Tester:             name and email

-------------------------------------------------------------------------------
TESTING SCHEDULE
Break the testing down into phases (ex. Planning, Case Design, Unit & Component
Tests, Integration Tests, Stabilization, Performance and Capacity Tuning, Full 
Pass and Shipping) - and make a rough schedule of sequence and dates. What tasks 
do you plan on having done in what phases? This is a brief, high level summary - 
just to set expectation that certain components will be worked on at certain 
times - and to indicate that the plan is taking project schedule concerns into 
consideration.
Include a pointer to more detailed feature and team schedules here.

-------------------------------------------------------------------------------
FEATURE HISTORY
A history of how the feature was designed, and evolved, over time. It is a good 
idea to build this history up as test plans go. This gives a good feel for why 
the current release is focusing on what it has done. It also serves a good 
framework for where problems have been in the past.
A paragraph or two is probably sufficient for each drop, indicating - original 
intent, feedback and successes, problems, resolutions, things learned from the 
release, major issues dealt with or discovered in the release.
Basically, this section is a mini post-mortem. It is eventually finishes with 
a statement regarding the development of the specific version.
It is often helpful to update this history at each milestone of a project.

-------------------------------------------------------------------------------
FEATURES:
This section gives a breakdown of the areas of the feature. It is often useful 
to include in this section a per area statement of testing's thoughts. What 
type of testing is best used for each area? What is problematic about each 
area? Has this area had a problem in the past. Quick statements are all that 
is need in this list.

NOTE: this is only here as a high level summary of the features. The real meat 
is in the area breakdown. This is a tad redundant in that respect...

-------------------------------------------------------------------------------
FILES AND MODULES:
Include in this section any files, modules and code that must be distributed 
on the machine, and where they would be located. Also include registry 
settings, INI settings, setup procedures, de-installation procedures, special 
database and utility setups, and any other relevant data.

- FILES LIST:
- REGISTRY, INI SETTINGS:
- SETUP PROCEDURES:
- DE-INSTALLATION PROCEDURES
- DATABASE SETUP AND PROCEDURES
- NETWORK DOMAIN/TOPOLOGIES CONFIGURATION PROCEDURES
- PERFORMANCE MONITORING COUNTERS SETUP AND CONFIGURATIONS

-------------------------------------------------------------------------------
OPERATIONAL ISSUES
Is the program being monitored/maintained by an operational staff? Are there 
special problem escalation, or operational procedures for dealing with the 
feature/program/area?

- BACKUP
- RECOVERY
- ARCHIVING
- MONITORING
- OPERATIONAL PROBLEM ESCALATION/ALERT METHODS

-------------------------------------------------------------------------------
SCOPE OF TEST CASES
Statement regarding the degree and types of coverage the testing will involve. 
For example, will focus be placed on performance? How about client v.s. server 
issues? Is there a large class of testing coverage that will be intentionally 
overlooked or minimized? Will there be much unit and component testing? This is 
a big sweeping picture of the testing coverage - giving an overall statement 
of the testing scope.

-------------------------------------------------------------------------------
ACCEPTANCE CRITERIA
How is "Good Enough To Ship" defined for the project? For the feature? What are 
the necessary performance, stability and bug find/fix rates to determine that 
the product is ready to ship?

-------------------------------------------------------------------------------
KEY FEATURE ISSUES
What are the top problems/issues that are recurring or remain open in this test 
plan? What problems remain unresolved?

-------------------------------------------------------------------------------
TEST APPROACH
- DESIGN VALIDATION
  Statements regarding coverage of the feature design - including both 
  specification and development documents. Will testing review design? Is 
  design an issue on this release? How much concern does testing have regarding 
  design, etc. etc..
- DATA VALIDATION
  What types of data will require validation? What parts of the feature will 
  use what types of data? What are the data types that test cases will 
  address? Etc.  
- API TESTING
  What level of API testing will be performed? What is justification for taking 
  this approach (only if none is being taken)?
- CONTENT TESTING
  Is your area/feature/product content based? What is the nature of the content? 
  What strategies will be employed in your feature/area to address content 
  related issues?
- LOW-RESOURCE TESTING
  What resources does your feature use? Which are used most, and are most likely 
  to cause problems? What tools/methods will be used in testing to cover low 
  resource (memory, disk, etc.) issues?
- SETUP TESTING
  How is your feature affected by setup? What are the necessary requirements for 
  a successful setup of your feature? What is the testing approach that will be 
  employed to confirm valid setup of the feature?
- MODES AND RUNTIME OPTIONS
  What are the different run time modes the program can be in? Are there views 
  that can be turned off and on? Controls that toggle visibility states? Are there 
  options a user can set which will affect the run of the program? List here the 
  different run time states and options the program has available. It may be 
  worthwhile to indicate here which ones demonstrate a need for more testing 
  focus.
- INTEROPERABILITY
  How will this product interact with other products?  What level of knowledge 
  does it need to have about other programs -- "good neighbor", program 
  cognizant, program interaction, fundamental system changes?  What methods 
  will be used to verify these capabilities?
- INTEGRATION TESTING
  Go through each area in the product and determine how it might interact with 
  other aspects of the project.  Start with the ones that are obviously 
  connected, but try every area to some degree.  There may be subtle connections 
  you do not think about until you start using the features together.  The test 
  cases created with this approach may duplicate the modes and objects 
  approaches, but there are some areas which do not fit in those categories and 
  might be missed if you do not check each area.
- COMPATIBILITY: CLIENTS
  Is your feature a server based component that interacts with clients? Is 
  there a standard protocol that many clients are expected to use? How many 
  and which clients are expected to use your feature? How will you approach 
  testing client compatibility? Is your server suited to handle ill-behaved 
  clients? Are there subtleties in the interpretation of standard protocols 
  that might cause incompatibilities? Are there non-standard, but widely 
  practiced use of your protocols that might cause incompatibilities?
- COMPATIBILITY: SERVERS
  Is your feature a client based component that interacts with servers? Is 
  there a standard protocol supported by many servers that your client speaks? 
  How many different servers will your client program need to support? 
  How will you approach testing server compatibility? Is your client suited 
  to handle ill-behaved or non-standard servers? Are there subtleties in the 
  interpretation of standard protocols that might cause incompatibilities? 
  Are there non-standard, but widely practiced use of protocols that might 
  cause incompatibilities?
- BETA TESTING
  What is the beta schedule? What is the distribution scale of the beta? What 
  is the entry criteria for beta? How is testing planning on utilizing the beta 
  for feedback on this feature? What problems do you anticipate discovering in 
  the beta? Who is coordinating the beta, and how?
- ENVIRONMENT/SYSTEM - GENERAL
  Are there issues regarding the environment, system, or platform that should 
  get special attention in the test plan? What are the run time modes and 
  options in the environment that may cause difference in the feature? List 
  the components of critical concern here. Are there platform or system 
  specific compliance issues that must be maintained?
- CONFIGURATION
  Are there configuration issues regarding hardware and software in the 
  environment that may get special attention in the test plan? Some of the 
  classical issues are machine and bios types, printers, modems, video 
  cards and drivers, special or popular TSR's, memory managers, networks, etc. 
  List those types of configurations that will need special attention.
- USER INTERFACE
  List the items in the feature that explicitly require a user interface. Is 
  the user interface designed such that a user will be able to use the feature 
  satisfactorily? Which part of the user interface is most likely to have bugs? 
  How will the interface testing be approached?
- PERFORMANCE & CAPACITY TESTING
  How fast and how much can the feature do? Does it do enough fast enough? What 
  testing methodology will be used to determine this information? What criterion 
  will be used to indicate acceptable performance? If modifications of an existing 
  product, what are the current metrics? What are the expected major bottlenecks 
  and performance problem areas on this feature?
- PRIVACY
  Are there test in place that validate the protection of customer data? How does 
  the software manage the collection, storage, and sharing of data? Are privacy 
  choices properly administered for users, enterprise, parent controls? Are all 
  transmissions of sensitive data encrypted to avoid the inadvertent exposure of 
  data? Does the application should provide a mechanism to mange its privacy settings 
  during the install, first-run experience, or before data is ever transmitted from a 
  user's machine.
- RELIABILITY
  How does the product meet the reliability and performance goals, under the desired 
  operational profile? The test team should plan for long-haul testing. How will code 
  coverage be used to measure the effectiveness of your tests? What is the expectations 
  for reliability when tested under low-resources, unavailable-resources, and 
  high-latency conditions. Do users get feedback on long-latency operations? Are such 
  operations cancelable? Do the right events and their information get logged through 
  your customer feedback mechanisms?
- SCALABILITY
  Is the ability to scale and expand this feature a major requirement? What parts of 
  the feature are most likely to have scalability problems? What approach will testing 
  use to define the scalability issues in the feature?
- STRESS TESTING
  How does the feature do when pushed beyond its performance and capacity limits? 
  How is its recovery? What is its breakpoint? What is the user experience when 
  this occurs? What is the expected behavior when the client reaches stress levels? 
  What testing methodology will be used to determine this information? What area 
  is expected to have the most stress related problems?
- VOLUME TESTING
  Volume testing differs from performance and stress testing in so much as it 
  focuses on doing volumes of work in realistic environments, durations, and 
  configurations. Run the software as expected user will - with certain other 
  components running, or for so many hours, or with data sets of a certain size, 
  or with certain expected number of repetitions.
- INTERNATIONAL ISSUES
  Confirm localized functionality, that strings are localized and that code 
  pages are mapped properly. Assure program works properly on localized builds, 
  and that international settings in the program and environment do not break 
  functionality. How is localization and internationalization being done on 
  this project? List those parts of the feature that are most likely to be 
  affected by localization. State methodology used to verify International 
  sufficiency and localization.
- ROBUSTNESS
  How stable is the code base? Does it break easily? Are there memory leaks? 
  Are there portions of code prone to crash, save failure, or data corruption? 
  How good is the program's recovery when these problems occur? How is the 
  user affected when the program behaves incorrectly? What is the testing 
  approach to find these problem areas? What is the overall robustness goal 
  and criteria?
- ERROR TESTING
  How does the program handle error conditions? List the possible error 
  conditions. What testing methodology will be used to evoke and determine 
  proper behavior for error conditions? What feedback mechanism is being 
  given to the user, and is it sufficient? What criteria will be used to 
  define sufficient error recovery?
- USABILITY
  What are the major usability issues on the feature? What is testing's 
  approach to discover more problems? What sorts of usability tests and 
  studies have been performed, or will be performed? What is the usability 
  goal and criteria for this feature?
- ACCESSIBILITY
  Is the feature designed in compliance with accessibility guidelines? Could a 
  user with special accessibility requirements still be able to utilize this 
  feature? What is the criteria for acceptance on accessibility issues on this 
  feature? What is the testing approach to discover problems and issues? Are 
  there particular parts of the feature that are more problematic than others?
- USER SCENARIOS
  What real world user activities are you going to try to mimic? What classes 
  of users (i.e. secretaries, artist, writers, animators, construction worker, 
  airline pilot, shoemaker, etc.) are expected to use this program, and doing 
  which activities? How will you attempt to mimic these key scenarios? Are 
  there special niche markets that your product is aimed at (intentionally 
  or unintentionally) where mimic real user scenarios is critical?
- BOUNDARIES AND LIMITS
  Are there particular boundaries and limits inherent in the feature or area 
  that deserve special mention here? What is the testing methodology to discover 
  problems handling these boundaries and limits?
- OPERATIONAL ISSUES
  If your program is being deployed in a data center, or as part of a 
  customer's operational facility, then testing must, in the very least, 
  mimic the user scenario of performing basic operational tasks with the 
  software.
  - BACKUP
    Identify all files representing data and machine state, and indicate how 
    those will be backed up. If it is imperative that service remain running, 
    determine whether or not it is possible to backup the data and still keep 
    services or code running.
  - RECOVERY
    If the program goes down, or must be shut down, are there steps and 
    procedures that will restore program state and get the program or service 
    operational again? Are there holes in this process that may make a service 
    or state deficient? Are there holes that could provide loss of data. Mimic 
    as many states of loss of services that are likely to happen, and go through 
    the process of successfully restoring service.
  - ARCHIVING
    Archival is different from backup. Backup is when data is saved in order to 
    restore service or program state. Archive is when data is saved for 
    retrieval later. Most archival and backup systems piggy-back on each 
    other's processes.
    Is archival of data going to be considered a crucial operational issue on 
    your feature? If so, is it possible to archive the data without taking the 
    service down? Is the data, once archived, readily accessible?
  - MONITORING
    Does the service have adequate monitoring messages to indicate status, 
    performance, or error conditions? When something goes wrong, are messages 
    sufficient for operational staff to know what to do to restore proper 
    functionality? Are the "hearbeat" counters that indicate whether or not the 
    program or service is working? Attempt to mimic the scenario of an 
    operational staff trying to keep a service up and running.
  - UPGRADE
    Does the customer likely have a previous version of your software, or some 
    other software? Will they be performing an upgrade? Can the upgrade take 
    place without interrupting service? Will anything be lost (functionality, 
    state, data) in the upgrade? Does it take unreasonably long to upgrade the service?
  - MIGRATION
    Is there data, script, code or other artifacts from previous versions that 
    will need to be migrated to a new version? Testing should create an example 
    of installation with an old version, and migrate that example to the new 
    version, moving all data and scripts into the new format.
    List here all data files, formats, or code that would be affected by 
    migration, the solution for migration, and how testing will approach each.
- SPECIAL CODE PROFILING AND OTHER METRICS
  How much focus will be placed on code coverage? What tools and methods will 
  be used to measure the degree to which testing coverage is sufficiently 
  addressing all of the code?

-------------------------------------------------------------------------------
TEST ENVIRONMENT
What are the requirements for the product?  They should be reflected in the 
breadth of  hardware configuration testing.

- OPERATING SYSTEMS
  Identify all operating systems under which this product will run.  Include 
  version numbers if applicable.
- NETWORKS
  Identify all networks under which this product will run.  include version 
  numbers if applicable.
- HARDWARE
  Identify the various hardware platforms and configurations.
  - MACHINES
  - GRAPHICS ADAPTERS
  - EXTENDED AND EXPANDED MEMORY BOARDS
  - OTHER PERIPHERAL
    Peripherals include those necessary for testing such as CD-ROM, printers, 
    modems, faxes, external hard drive, tape readers, etc.
- SOFTWARE
  Identify software included with the product or likely to be used in 
  conjunction with this product.  Software categories would include memory 
  managers, extenders, some TSRs, related tools or products, or similar 
  category products.

-------------------------------------------------------------------------------
UNIQUE TESTING CONCERNS FOR SPECIFIC FEATURES
List specific features which may require more attention than others, and 
describe how testing will approach these features. This is to serve as a sort 
of "hot list".

-------------------------------------------------------------------------------
AREA BREAKDOWN
This is a detailed breakdown of the feature or area - and is best done in an 
outline format. It is useful as a tool later when building test cases. The 
outline of an area can go on quite long. Usually it starts with a menu 
breakdown, and then continues on with those features and functionalities not 
found on any menu in particular.

- FEATURE NAME
  - SUB FEATURE ONE
    - SUB 1.1
      Feature testing approach matrix: this will repeat for each subitem, 
      including any class of testing relevant to any item. Put in NA if not 
      applicable. Location of this matrix in the hierarchy determines scope. 
      For example, data validation rules global to anything under Sub Feature 
      One should go under "Sub Feature One". Inheritance should be implied.
      
      CLASS                                     |INFO and AUTOMATED OR MANUAL
      Design Validation                         |            
      Data Validation                           |Valid data & expected results (e.g. "alphanumeric"). Invalid data & 
                                                 expected results. (e.g. "no ';', '/' or '@') How to validate?
      API Testing                               |What are the API's exposed? What are the permutations of calling these 
                                                 API's (order, specific args, etc.)?        
      Content Testing                           |What content exercises this feature? What content does this feature 
                                                 produce, modify or manage?        
      Low-Resource Testing                      |What resource dimensions to test? What to do when resource is low?        
      Setup Testing                             |What types of setups? How to confirm feature after a setup?        
      Modes & Runtime Options                   |What modes and runtime options does this have? What should be tested 
                                                 during these modes? What are expected results in different modes?        
      Interoperability                          |What do we interoperate with? Do what action with it?        
      Integration Testing                       |What do we integrate with? Do what action with it?        
      Compatibility: Clients                    |What clients? Doing what actions?        
      Compatibility: Servers                    |What servers? Doing what actions?        
      Beta Testing                              |
      Environment/System                        |What environmental issues apply to this? What to do to expose?        
      Configuration                             |What environmental issues apply to this? What to do to expose?        
      User Interface                            |What are the interface points? How to exercise them?        
      Performance                               |What are the target performance dimensions? What will you do to 
                                                 exercise these?
      Capacity                                  |What is the target capacity? What will you do to test this?
      Scalability                               |What is the target scale, and how? What will you do to test this?
      Stress                                    |What dimensions do you plan on stressing? What is expectation? How 
                                                 will you stress it?        
      Volume Tests                              |What actions will be included in volume tests?        
      International                             |What are the international problems of this item?        
      Robustness                                |What robustness (crashes, corruption, etc.) errors are anticipated? How 
                                                 will you look for them?        
      Error Testing                             |What are the relevant error conditions that the program expects? What 
                                                 are the error situations you plan on simulating?        
      Usability                                 |What are the usability issues about this item?        
      Accessibility                             |What are the accessibility issues about this item?        
      User Scenarios                            |How would a user typically use this item? What tests will you do to 
                                                 simulate user scenarios?        
      Boundaries and Limits                     |What are the boundary conditions surrounding this item?
      What are the limits of this item?         |
      Max Values? Minimum Values?               |
      Special Code Profiling and Other Metrics  |
      Schedule                                  |When?        
      Code Paths and Sequences?                 |What are the different ways to invoke or activate this item? What are 
                                                 things you can do just before this item that are supposed to change 
                                                 the way it operates? What should NOT change the way it operates?        

    - SUB 1.2
    - SUB 1.3
  - SUB FEATURE TWO
  - SUB FEATURE THREE (ETC.)

-------------------------------------------------------------------------------
TEST CASE STRUCTURE
Where will test cases be stored? What is the naming scheme? What is the 
organizing structure? How do test cases correlate to the test plan?

-------------------------------------------------------------------------------
SPEC REVIEW ISSUES
Indicate location and method being used for reporting problems against the 
specification and design.

-------------------------------------------------------------------------------
TEST TOOLS
List whatever test tools will be used, or need to be written, and for what 
purpose. It is often best to point to an external location for more details, 
as tools usually require an entire plan and architectural summary of their 
own.

-------------------------------------------------------------------------------
SMOKE TEST (ACCEPTANCE TEST, BUILD VERIFICATION, ETC.)
The smoke test determines whether or not the build is good enough to be 
submitted to testing. This section gives a statement of what the basic smoke 
test consists of, how it is design, and how it will be performed. A pointer to 
suite locations is helpful here too.

-------------------------------------------------------------------------------
AUTOMATED TESTS
What degree of automation will be used testing this area? What platform/tools 
will be used to write the automated tests? What will the automation focus on? 
Where are the automated tools, suites and sources checked in?

-------------------------------------------------------------------------------
MANUAL TESTS
What sorts of items will be tested manually rather than via automation? Why is 
manual testing being chosen over automation? Where are the manual tests defined 
and located?

-------------------------------------------------------------------------------
REGRESSION TESTS
What is your general regression strategy? Are you going to automate? Where are 
the regressions stored? How often will they be re-run?

-------------------------------------------------------------------------------
BUG BASHES
What is your strategy for bug bashes? How many? What goals? What incentives? 
What areas are targetted to be bashed? By who?

-------------------------------------------------------------------------------
BUG REPORTING
What tool(s) will be used to report bugs, and where are the bug reports 
located? Are there any special pieces of information regarding categorization 
of bugs that should be reported here (areas, keywords, etc.)?

-------------------------------------------------------------------------------
PLAN CONTINGENCIES
Is there anything that may require testing's plans to change? Briefly describe 
how you plan to react to those changes.

-------------------------------------------------------------------------------
EXTERNAL DEPENDENCIES
Are there any groups or projects external to the team that are dependent on 
your feature, or that your feature is dependent on? What testing problems and 
issues does this create? How are deliverables from external groups going to 
be tested and confirmed with your own feature? Who are the individuals serving 
as primary contact and liaison in this relationship?

-------------------------------------------------------------------------------
HEADCOUNT REQUIREMENTS
How many people will it require to implement these plans? Are there currently 
enough people on staff right now? What effect will hiring more or less people 
have (slip in schedule, quality, or something else?).

-------------------------------------------------------------------------------
PRODUCT SUPPORT
What aspects of this feature have been a problem for support in the past? How 
are those problems being addressed? What aspects of this feature will likely 
cause future support problems? How are those problems being resolved? What 
testing methodology is being used to prevent future support problems? How is 
information being sent to support regarding problems and functionality of the 
feature?

-------------------------------------------------------------------------------
TESTING SCHEDULE
Break the testing down into phases (ex. Planning, Case Design, Unit & Component 
Tests, Integration Tests, Stabilization, Performance and Capacity Tuning, Full 
Pass and Shipping) - and make a rough schedule of sequence and dates. What tasks 
do you plan on having done in what phases? This is a brief, high level summary - 
just to set expectation that certain components will be worked on at certain 
times - and to indicate that the plan is taking project schedule concerns into 
consideration.
Include a pointer to more detailed feature and team schedules here.

-------------------------------------------------------------------------------
DROP PROCEDURES
Define the methodology for handing off the code between Development and Testing.

-------------------------------------------------------------------------------
RELEASE PROCEDURES
Describe the step-wise process for getting the product from the network testing 
version to ready-to-ship master diskette sets.

-------------------------------------------------------------------------------
ALIAS/NEWSGROUPS AND COMMUNICATION CHANNELS
List any email aliases and what they are for. List any bulletin boards, 
newsgroups, or other communication procedures and methodologies here.

-------------------------------------------------------------------------------
REGULAR MEETINGS
For each meeting, when, where, what is general agenda.

- FEATURE TEAM MEETINGS
- PROJECT TEST TEAM MEETINGS
- FEATURE TEAM TEST MEETINGS

-------------------------------------------------------------------------------
DECISIONS MAKING PROCEDURES
Who reviews decision points for the following sorts of things: build approval, 
bug triage, feature sign off, test plan sign off, development design sign off? 
What is the process for the various decisions?

-------------------------------------------------------------------------------
NOTES
'''
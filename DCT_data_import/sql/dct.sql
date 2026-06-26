create table db_key
(
    id            int auto_increment
        primary key,
    datetime      int               null,
    db_key        varchar(255)      null,
    recovery_rate tinyint default 0 not null,
    tester        tinyint default 0 not null,
    test_result   tinyint default 0 not null,
    fail_pin      tinyint default 0 not null,
    check_status  tinyint default 0 not null,
    import_status tinyint default 0 not null,
    mail          tinyint default 0 not null,
    remark        varchar(255)      null
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create table db_key_ui_status
(
    id            int auto_increment
        primary key,
    datetime      int               null,
    db_key        varchar(255)      null,
    ui_status     tinyint default 0 not null,
    check_status  tinyint default 0 not null,
    import_status tinyint default 0 not null,
    mail          tinyint default 0 not null,
    remark        varchar(255)      null
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create table detection_methods
(
    id          tinyint unsigned auto_increment
        primary key,
    method_key  varchar(20)                        not null,
    method_name varchar(50)                        not null,
    created_at  datetime default CURRENT_TIMESTAMP null,
    updated_at  datetime default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP,
    constraint method_key
        unique (method_key)
)
    collate = utf8mb4_unicode_ci;

create table detection_specs
(
    id                   bigint auto_increment
        primary key,
    program              varchar(100)                       not null,
    test_item_name       varchar(100)                       null,
    site_id              int unsigned                       not null,
    detection_method_id  tinyint unsigned                   not null,
    spec_upper_limit     decimal(18, 9)                     null,
    spec_lower_limit     decimal(18, 9)                     null,
    spec_calc_start_time datetime                           not null,
    spec_calc_end_time   datetime                           not null,
    spec_calc_mean       decimal(18, 9)                     null,
    spec_calc_std        decimal(18, 9)                     null,
    created_at           datetime default CURRENT_TIMESTAMP null,
    updated_at           datetime default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP,
    constraint fk_specs_detection_method
        foreign key (detection_method_id) references detection_methods (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci;

create index idx_program_item_method
    on detection_specs (program, test_item_name, detection_method_id);

create index idx_program_method
    on detection_specs (program, detection_method_id);

create table fail_pin_rate_info
(
    id          int auto_increment
        primary key,
    mac_address varchar(20)  null,
    db_key      varchar(255) null comment 'db_version',
    area        varchar(20)  null,
    factory     varchar(20)  null,
    os_machine  varchar(20)  null,
    ao_lot      varchar(50)  null,
    mode        varchar(20)  null,
    data_format varchar(10)  null,
    file_name   varchar(50)  null,
    date        datetime     null,
    total       int          null,
    pass        int          null,
    open        int          null,
    short       int          null,
    lk          int          null,
    nVTEP       int          null
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index IDX_FAIL_PIN_RATE_INFO_DB_KEY
    on fail_pin_rate_info (db_key);

create table fail_pin_rate_list
(
    id                    int auto_increment
        primary key,
    fail_pin_rate_info_id int         null,
    dut                   int         null,
    sn_num                varchar(50) null,
    site                  int         null,
    fail_type             varchar(50) null,
    constraint fail_pin_rate_list_fail_pin_rate_info
        foreign key (fail_pin_rate_info_id) references fail_pin_rate_info (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index fail_pin_rate_list_fail_pin_rate_info_id
    on fail_pin_rate_list (fail_pin_rate_info_id);

create table fail_pin_rate_list_pin_ball
(
    id                    int auto_increment
        primary key,
    fail_pin_rate_list_id int          null,
    pin                   varchar(100) null,
    ball                  varchar(10)  null,
    remark                varchar(100) null,
    constraint fail_pin_rate_list_pin_ball_fail_pin_rate_list
        foreign key (fail_pin_rate_list_id) references fail_pin_rate_list (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index fail_pin_rate_list_pin_ball_fail_pin_rate_list_id
    on fail_pin_rate_list_pin_ball (fail_pin_rate_list_id);

create table fail_pin_rate_test_result
(
    id                    int auto_increment
        primary key,
    fail_pin_rate_list_id int                    null,
    item_name             varchar(50) default '' null,
    open                  double                 null,
    short                 double                 null,
    vmeas                 double                 null,
    constraint fail_pin_rate_test_result_fail_pin_rate_list
        foreign key (fail_pin_rate_list_id) references fail_pin_rate_list (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index fail_pin_rate_test_result_fail_pin_rate_list_id
    on fail_pin_rate_test_result (fail_pin_rate_list_id);

create table ieda_content
(
    id                          int auto_increment
        primary key,
    title_id                    int                    null,
    touch_down                  int                    null,
    sw_bin                      int                    null,
    vi_result                   int                    null,
    site_index                  int                    null,
    index_time                  double                 null,
    test_time                   double                 null,
    re_probing_flag_retest_flag int                    null,
    handler_arm                 int                    null,
    temperature                 varchar(8)  default '' null,
    package_start_time          datetime               null,
    handler_arm_force           varchar(32)            null,
    wafer_id                    varchar(12) default '' null,
    wafer_x                     varchar(16)            null,
    wafer_y                     varchar(16)            null,
    serial_number               int                    null,
    efuse_string_1              varchar(53) default '' null,
    efuse_string_2              varchar(64) default '' null,
    efuse_string_3              varchar(46) default '' null,
    efuse_string_4              varchar(37) default '' null,
    spare_para_1                varchar(16)            null,
    spare_para_2                varchar(16)            null,
    spare_para_3                varchar(16)            null,
    spare_para_4                varchar(20) default '' null,
    soft_bin_name               varchar(20) default '' null,
    hard_bin_number             int                    null,
    hard_bin_name               varchar(20) default '' null,
    ocr_laser_mark_qr_code      varchar(25) default '' null
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index ieda_content_title_id
    on ieda_content (title_id);

create table ieda_title
(
    id                  int auto_increment
        primary key,
    ase_lot             varchar(30) default '' null,
    lot_id              varchar(30) default '' null,
    sub_lot             varchar(10)            null,
    device              varchar(32) default '' null,
    mpw_code            varchar(16)            null,
    produce_code        varchar(10)            null,
    tester_id           varchar(8)  default '' null,
    oper_id             varchar(8)  default '' null,
    test_program        varchar(50) default '' null,
    start_time          datetime               null,
    end_time            datetime               null,
    socket_lid          varchar(12) default '' null,
    load_board_id       varchar(12) default '' null,
    bd_file             varchar(20) default '' null,
    package_notch       varchar(1)  default '' null,
    sort_stage          varchar(1)  default '' null,
    test_site           varchar(8)  default '' null,
    fd_file             varchar(20) default '' null,
    cover_id_side_blade varchar(20) default '' null,
    socket_id           varchar(20) default '' null,
    handler_id          varchar(20) default '' null,
    device_rev          varchar(10) default '' null,
    tsmc_lot_id         varchar(12) default '' null,
    assembly_start_date varchar(11) default '' null,
    assembly_end_date   varchar(11) default '' null
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index ieda_title_ase_lot
    on ieda_title (ase_lot);

create table lots_info
(
    id                   int auto_increment
        primary key,
    version              varchar(255) null,
    mac_address          varchar(255) null,
    db_key               varchar(255) null,
    customer             varchar(255) null,
    package              varchar(255) null,
    bonding_diagram      varchar(255) null,
    program              varchar(255) null,
    device               varchar(255) null,
    control_lot          varchar(255) null,
    ao_lot               varchar(255) null,
    os_machine_id        varchar(255) null,
    os_test_board_id     varchar(255) null,
    user_id              varchar(255) null,
    schedule_lot         varchar(255) null,
    file_name            varchar(255) null,
    yield                double       null,
    total                int          null,
    pass                 int          null,
    open_pin_fail        int          null,
    short_pin_fail       int          null,
    leakage_pin_fail     int          null,
    nvtep_pin_fail       int          null,
    total_ppm            double       null,
    open_pin_fail_ppm    double       null,
    short_pin_fail_ppm   double       null,
    leakage_pin_fail_ppm double       null,
    nvtep_pin_fail_ppm   double       null,
    total_test_items     int          null,
    average_test_time    double       null,
    clear_count          double       null,
    start                datetime     null,
    stop                 datetime     null,
    pass_without_ocr     int          null,
    open                 int          null,
    open_without_ocr     int          null,
    short_others         int          null,
    pass_without_ocr_ppm double       null,
    open_ppm             double       null,
    open_without_ocr_ppm double       null,
    short_others_ppm     double       null,
    constraint file_name
        unique (file_name)
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create table anomaly_lots
(
    id                  bigint auto_increment
        primary key,
    lots_info_id        int                                not null,
    detection_method_id tinyint unsigned                   not null,
    detection_value     decimal(18, 9)                     null,
    offset_value        decimal(18, 9)                     null,
    spec_upper_limit    decimal(18, 9)                     null,
    spec_lower_limit    decimal(18, 9)                     null,
    created_at          datetime default CURRENT_TIMESTAMP null,
    updated_at          datetime default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP,
    constraint unq_lot_method
        unique (lots_info_id, detection_method_id),
    constraint fk_anomaly_lots_detection_method
        foreign key (detection_method_id) references detection_methods (id)
            on update cascade on delete cascade,
    constraint fk_anomaly_lots_info
        foreign key (lots_info_id) references lots_info (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci;

create table anomaly_lot_process_mapping
(
    id             bigint auto_increment
        primary key,
    anomaly_lot_id bigint                             not null,
    plant_name     varchar(100)                       null,
    station_name   varchar(100)                       null,
    machine_id     varchar(50)                        null,
    trackin_user   varchar(50)                        null,
    trackout_user  varchar(50)                        null,
    recipe         varchar(50)                        null,
    created_at     datetime default CURRENT_TIMESTAMP null,
    updated_at     datetime default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP,
    constraint fk_lot_process_anomaly_lot
        foreign key (anomaly_lot_id) references anomaly_lots (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci;

create table anomaly_units
(
    id               bigint auto_increment
        primary key,
    anomaly_lot_id   bigint                                not null,
    test_item_name   varchar(100)                          not null,
    site_id          int unsigned                          not null,
    unit_id          varchar(50) default ''                not null,
    detection_value  decimal(18, 9)                        null,
    offset_value     decimal(18, 9)                        null,
    spec_upper_limit decimal(18, 9)                        null,
    spec_lower_limit decimal(18, 9)                        null,
    created_at       datetime    default CURRENT_TIMESTAMP null,
    updated_at       datetime    default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP,
    constraint unq_lot_item_unit
        unique (anomaly_lot_id, test_item_name, unit_id),
    constraint fk_units_anomaly_lot
        foreign key (anomaly_lot_id) references anomaly_lots (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci;

create table anomaly_unit_process_mapping
(
    id              bigint auto_increment
        primary key,
    anomaly_unit_id bigint                             not null,
    boat_id         varchar(50)                        not null,
    boat_x          smallint                           not null,
    boat_y          smallint                           not null,
    wafer_barcode   varchar(50)                        not null,
    wafer_id        varchar(50)                        not null,
    wafer_x         smallint                           not null,
    wafer_y         smallint                           not null,
    substrate_id    varchar(50)                        not null,
    substrate_x     smallint                           not null,
    substrate_y     smallint                           not null,
    wafer_max_x     smallint                           not null,
    wafer_max_y     smallint                           not null,
    boat_max_x      smallint                           not null,
    boat_max_y      smallint                           not null,
    plant_name      varchar(100)                       null,
    station_name    varchar(100)                       null,
    equipment_id    varchar(50)                        null,
    created_at      datetime default CURRENT_TIMESTAMP null,
    updated_at      datetime default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP,
    constraint fk_unit_process_anomaly_unit
        foreign key (anomaly_unit_id) references anomaly_units (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci;

create table good_lots
(
    id                  bigint auto_increment
        primary key,
    lots_info_id        int                                not null,
    detection_method_id tinyint unsigned                   not null,
    created_at          datetime default CURRENT_TIMESTAMP null,
    updated_at          datetime default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP,
    constraint unq_lot_method
        unique (lots_info_id, detection_method_id),
    constraint fk_good_lots_detection_method
        foreign key (detection_method_id) references detection_methods (id)
            on update cascade on delete cascade,
    constraint fk_good_lots_info
        foreign key (lots_info_id) references lots_info (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci;

create index IDX_LOTS_INFO_DB_KEY
    on lots_info (db_key);

create table lots_result
(
    id          bigint auto_increment
        primary key,
    lot_id      int                                  null,
    serial      int                                  null,
    retest_loc  varchar(2) default ''                not null,
    sn_num      varchar(50)                          null,
    site_id     int                                  null,
    x           int                                  null,
    y           int                                  null,
    hbin        varchar(30)                          null,
    `pass/fail` enum ('pass', 'fail')                null,
    test_time   double                               null,
    index_time  double                               null,
    real_time   datetime   default CURRENT_TIMESTAMP null,
    constraint lots_result_ibfk_1
        foreign key (lot_id) references lots_info (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index lot_id
    on lots_result (lot_id);

create table lots_statistic
(
    id        bigint auto_increment
        primary key,
    lot_id    int                    null,
    site_id   int                    null,
    item_no   int                    null,
    item_name varchar(50) default '' null,
    net_name  varchar(50) default '' null,
    `force`   double                 null,
    wait_time double                 null,
    spec_max  double                 null,
    spec_min  double                 null,
    pass      int                    null,
    pass_n    int                    null,
    fail      int                    null,
    min       double                 null,
    max       double                 null,
    avg       double                 null,
    avg_2     double                 null comment '平方和平均',
    stdev     double                 null,
    cp        double                 null,
    cpk       double                 null,
    unit      varchar(20) default '' null,
    value     json                   null,
    constraint lots_statistic_lot_id
        foreign key (lot_id) references lots_info (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci
    row_format = COMPRESSED;

create index item_no
    on lots_statistic (item_no);

create table recovery_rate
(
    id             int auto_increment
        primary key,
    db_key         varchar(255)     null,
    area           varchar(20)      null,
    factory        varchar(20)      null,
    os_machine     varchar(20)      null,
    customer       varchar(20)      null,
    program        varchar(255)     null,
    ao_lot         varchar(20)      null,
    mode           varchar(20)      null,
    date           datetime         null,
    test_item      varchar(50)      null,
    defect_mode    varchar(20)      null,
    re_test_pass   varchar(20)      null,
    fail_pin_count int    default 0 null,
    total_unit     int    default 0 null,
    recovery_rate  double default 0 null
)
    collate = utf8mb4_unicode_ci;

create index IDX_RECOVERY_RATE_DB_KEY
    on recovery_rate (db_key);

create table site_test_statistics
(
    id             bigint auto_increment
        primary key,
    lots_info_id   int                                not null,
    program        varchar(100)                       not null,
    site_id        int unsigned                       not null,
    test_item_name varchar(100)                       not null,
    mean_value     decimal(18, 9)                     null,
    max_value      decimal(18, 9)                     null,
    min_value      decimal(18, 9)                     null,
    std_value      decimal(18, 9)                     null,
    tester_id      varchar(50)                        null,
    start_time     datetime                           null,
    end_time       datetime                           null,
    created_at     datetime default CURRENT_TIMESTAMP null,
    updated_at     datetime default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP,
    constraint unq_lot_site_item
        unique (lots_info_id, site_id, test_item_name),
    constraint fk_site_test_statistics_lots_info
        foreign key (lots_info_id) references lots_info (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci;

create index idx_program_site_item_time
    on site_test_statistics (program, site_id, test_item_name, start_time);

create index idx_start_time
    on site_test_statistics (start_time);

create table tester_device_info
(
    id                              int auto_increment
        primary key,
    db_key                          varchar(255) default 'N/A' null,
    mac_address                     varchar(20)                null,
    ip_address                      varchar(20)                null,
    area                            varchar(50)                null,
    factory                         varchar(20)                null,
    machine_type                    varchar(20)                null,
    machine_id                      varchar(50)                null,
    customer                        varchar(20)                null,
    device_production               varchar(50)                null,
    device_engineer                 varchar(50)                null,
    test_program                    varchar(255)               null,
    program_path                    varchar(255)               null,
    lot_id                          varchar(50)                null,
    wafer_id                        varchar(50)                null,
    execution_mode                  varchar(20)                null,
    `prober/handler`                varchar(50)                null,
    `L/B_id`                        varchar(128)               null,
    dut_board_type                  varchar(20)                null,
    efficiency_check                int                        null,
    ui_flow_checksum                int                        null,
    yield                           double                     null,
    file_type                       varchar(20)                null,
    start_time                      datetime                   null,
    end_time                        datetime                   null,
    lead_count                      int                        null,
    site_qty                        int                        null,
    bd_leak                         int                        null,
    pg_leak                         int                        null,
    wireclose_leak                  int                        null,
    handler_type                    varchar(20)                null,
    handler_sw_version              varchar(20)                null,
    handler_repair_start_time       varchar(20)                null,
    handler_repair_end_time         varchar(20)                null,
    doe_flag                        int                        null,
    hso_mode                        varchar(50)                null,
    mp_api_log                      int                        null,
    mp_tt_log                       int                        null,
    smart_delay_enable              int                        null,
    smart_delay_time                double                     null,
    atv_information                 int          default 0     null,
    NetlistInfo                     int                        null,
    TP_CheckerDetectionResults      int                        null,
    PG_LeakageEnabled               int                        null,
    LeakageEnabled                  int                        null,
    EnhanceTestTtemQTY              int                        null,
    First_Yield                     double                     null,
    shortFailAnalysisFlag           int                        null,
    OSVersion                       varchar(50)                null,
    DCT_Type                        varchar(50)                null,
    DCT_Qty                         int                        null,
    DCT_CH_Qty                      int                        null,
    LB_Type                         varchar(50)                null,
    ConnecterType                   varchar(50)                null,
    Short_Plate_Check_Status        varchar(64)                null,
    Short_Plate_Check_Pin_qty_match varchar(16)                null,
    TP_HighRiskLot                  int                        null,
    TP_WarningLot                   int                        null,
    TP_OverkillQTY                  int                        null
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index IDX_TESTER_DEVICE_INFO_DB_KEY
    on tester_device_info (db_key);

create index idx_area_factory_time
    on tester_device_info (area, factory, start_time);

create index idx_area_factory_tp_time
    on tester_device_info (area, factory, test_program, start_time);

create index idx_area_start_time
    on tester_device_info (area, start_time);

create table tester_production_analysis
(
    id             int auto_increment
        primary key,
    device_info_id int    null,
    site1_yield    double null,
    site2_yield    double null,
    site3_yield    double null,
    site4_yield    double null,
    site5_yield    double null,
    site6_yield    double null,
    site7_yield    double null,
    site8_yield    double null,
    site9_yield    double null,
    site10_yield   double null,
    site11_yield   double null,
    site12_yield   double null,
    site13_yield   double null,
    site14_yield   double null,
    site15_yield   double null,
    site16_yield   double null,
    constraint tester_device_info_tester_production_analysis
        foreign key (device_info_id) references tester_device_info (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create table tester_status
(
    id                                   int auto_increment
        primary key,
    device_info_id                       int                   null,
    dpw                                  varchar(20)           null,
    duts                                 int                   null,
    csv_name                             varchar(100)          null,
    uph                                  double                null,
    avg_test_time                        double                null,
    max_test_time                        double                null,
    min_test_time                        double                null,
    avg_index_test_time                  double                null,
    max_index_test_time                  double                null,
    min_index_test_time                  double                null,
    diff_time_die                        double                null,
    end_time_die                         double                null,
    first_time_die                       double                null,
    diff_time_file                       double                null,
    conclusion_file_path                 varchar(255)          null,
    raw_date_file_path                   varchar(255)          null,
    s2s_diff_file_path                   varchar(255)          null,
    `pass/fail`                          enum ('pass', 'fail') null,
    case_a_result                        varchar(20)           null,
    case_b_result                        varchar(20)           null,
    case_c_result                        varchar(20)           null,
    pui_result                           varchar(20)           null,
    pui_respond                          varchar(255)          null,
    pui_file_type                        varchar(20)           null,
    phi_result                           varchar(20)           null,
    phi_respond                          varchar(255)          null,
    phi_file_type                        varchar(20)           null,
    tp_result                            varchar(20)           null,
    tp_respond                           varchar(100)          null,
    manual_data_module_csv_g_result      varchar(20)           null,
    manual_data_module_csv_g_respond     varchar(20)           null,
    data_module_stdf_g_result            varchar(20)           null,
    data_module_stdf_g_respond           varchar(20)           null,
    data_module_txt_g_result             varchar(20)           null,
    data_module_txt_g_respond            varchar(20)           null,
    data_module_std_g_result             varchar(20)           null,
    data_module_std_g_respond            varchar(20)           null,
    test_time_module_csv_g_result        varchar(20)           null,
    test_time_module_csv_g_respond       varchar(20)           null,
    data_module_act_smart1_txt_result    varchar(20)           null,
    data_module_act_smart1_txt_respond   varchar(20)           null,
    data_module_asekh_smart1_xml_result  varchar(100)          null,
    data_module_asekh_smart1_xml_respond varchar(255)          null,
    data_module_act_fail_log_result      varchar(20)           null,
    data_module_act_fail_log_respond     varchar(20)           null,
    vim_result                           varchar(20)           null,
    vim_respond                          varchar(20)           null,
    vim_open_result                      varchar(20)           null,
    vim_open_respond                     varchar(20)           null,
    vicbit_result                        varchar(20)           null,
    vicbit_respond                       varchar(20)           null,
    vicbit_open_result                   varchar(20)           null,
    vicbit_open_respond                  varchar(20)           null,
    pattern_result                       varchar(20)           null,
    pattern_respond                      varchar(20)           null,
    SWT_result                           varchar(20)           null,
    SWT_respond                          varchar(20)           null,
    constraint tester_device_info_tester_status
        foreign key (device_info_id) references tester_device_info (id)
            on update cascade on delete cascade
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index idx_device_info_id
    on tester_status (device_info_id);

create table tester_sw_version
(
    id                                    int auto_increment
        primary key,
    device_info_id                        int         null,
    pui_version                           varchar(20) null,
    library_version                       varchar(50) null,
    virtual_instrument_version            varchar(50) null,
    mingw_version                         varchar(20) null,
    all_md5_checksum                      varchar(20) null,
    auto_learn_ui_md5                     varchar(32) null,
    dct_product_file_setting_ui_md5       varchar(32) null,
    dct_login_ui_md5                      varchar(32) null,
    os_self_diag_2k_md5                   varchar(32) null,
    pattonkan_ui_md5                      varchar(32) null,
    dct_iv_curve_tool_md5                 varchar(32) null,
    os_tester_100ma_vi_md5                varchar(32) null,
    os_tester_2a_vi_md5                   varchar(32) null,
    os_tester_lcr_meter_md5               varchar(32) null,
    wire_assignment_tool_md5              varchar(32) null,
    bga_highlight_tool_md5                varchar(32) null,
    simplification_ui_md5                 varchar(32) null,
    os_scan_tool_md5                      varchar(32) null,
    dct_uploadtp_ui_md5                   varchar(32) null,
    dct_autodownloadtp_md5                varchar(32) null,
    autolearn_dll_md5                     varchar(32) null,
    integration_simplified_tp_lib_dll_md5 varchar(32) null,
    libpublic_module_dll_md5              varchar(32) null,
    liblpsa_libs_dll_md5                  varchar(32) null,
    libvim_total_libs_dll_md5             varchar(32) null,
    slot0_dll_md5                         varchar(32) null,
    libtid_mem_libs_dll_md5               varchar(32) null,
    sys_tid_libs_dll_md5                  varchar(32) null,
    confirm_ftp_upload_dll_md5            varchar(32) null,
    confirm_ftp_upload_g_dll_md5          varchar(32) null,
    ctp_hontech_ttl_phi_dll_md5           varchar(32) null,
    data_module_asecl_assy_csv_dll_md5    varchar(32) null,
    data_module_asecl_fail_log_dll_md5    varchar(32) null,
    data_module_asecl_smart1_txt_dll_md5  varchar(32) null,
    data_module_asekh_a5_csv_dll_md5      varchar(32) null,
    data_module_asekh_a5_summary_dll_md5  varchar(32) null,
    data_module_asekh_change_kit_dll_md5  varchar(32) null,
    data_module_asekh_recoverrate_dll_md5 varchar(32) null,
    data_module_asekh_smart1_txt_dll_md5  varchar(32) null,
    data_module_asekh_smart1_xml_dll_md5  varchar(32) null,
    data_module_assy_txt_g_dll_md5        varchar(32) null,
    data_module_ca_csv_dll_md5            varchar(32) null,
    data_module_create_spec_dll_md5       varchar(32) null,
    data_module_csv_dll_md5               varchar(32) null,
    data_module_csv_g_dll_md5             varchar(32) null,
    data_module_intime_summary_dll_md5    varchar(32) null,
    data_module_std_dll_md5               varchar(32) null,
    data_module_stdf_dll_md5              varchar(32) null,
    data_module_stdf_g_dll_md5            varchar(32) null,
    data_module_std_g_dll_md5             varchar(32) null,
    data_module_txt_dll_md5               varchar(32) null,
    data_module_txt_g_dll_md5             varchar(32) null,
    hello_phi_dll_md5                     varchar(32) null,
    high_light_sys_dll_md5                varchar(32) null,
    hontech_phi_dll_md5                   varchar(32) null,
    k2_150a_dll_md5                       varchar(32) null,
    manual_data_module_csv_g_dll_md5      varchar(32) null,
    secs_gem_connect_dll_md5              varchar(32) null,
    srm_phi_dll_md5                       varchar(32) null,
    tel_p12_phi_dll_md5                   varchar(32) null,
    test_time_module_csv_g_dll_md5        varchar(32) null,
    rf_offset_data_module_csv_g_dll_md5   varchar(32) null,
    `2g_pat_module_inprocess_dma_dll_md5` varchar(32) null,
    `2g_multi_process_dll_md5`            varchar(32) null,
    auto_learn_pui_version                varchar(32) null
)
    collate = utf8mb4_unicode_ci
    row_format = DYNAMIC;

create index tester_device_info_tester_sw_version
    on tester_sw_version (device_info_id);

create table ui_status
(
    id                          int auto_increment
        primary key,
    mac_address                 varchar(50) null,
    area                        varchar(50) null,
    factory                     varchar(10) null,
    os_machine                  varchar(50) null,
    date                        datetime    null,
    auto_learn                  int         null,
    dct_product_file_setting_ui int         null,
    dct_login_ui                int         null,
    os_self_diag_2k             int         null,
    pattonkan_ui                int         null,
    dct_i_v_curve_tool          int         null,
    os_tester_100ma_vi          int         null,
    os_tester_2a_vi             int         null,
    os_tester_lcr_meter         int         null,
    wire_assignment_tool        int         null,
    bga_highlight_tool          int         null,
    simplificationui            int         null,
    os_scan_tool                int         null,
    dct_uploadtp_ui             int         null,
    dct_autodownloadtp          int         null,
    dct_sw_control_tool         int         null,
    dct_downloadtp_kh           int         null
)
    collate = utf8mb4_unicode_ci
    row_format = COMPRESSED;



#include <QCoreApplication>
#include "viewmodel/stationcontroller.h"

int main(int argc, char *argv[])
{
    QCoreApplication app(argc, argv);

    StationController controller;
    controller.setServerAddress("127.0.0.1", 16022);
    controller.start();

    return app.exec();
}
